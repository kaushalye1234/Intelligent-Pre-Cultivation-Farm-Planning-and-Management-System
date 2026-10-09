
import asyncio
import hashlib
import ipaddress
import json
import socket
from dataclasses import dataclass, field
from datetime import datetime, timezone
from io import BytesIO
from pathlib import Path
from typing import Awaitable, Callable
from urllib.parse import urljoin, urlsplit, urlunsplit

import httpx
from bs4 import BeautifulSoup
from pypdf import PdfReader

from config import Settings


class SourcePolicyError(ValueError):
    pass


class RetrievalError(RuntimeError):
    pass


@dataclass(frozen=True)
class SourcePolicyEntry:
    id: str
    organization_name: str
    approved_hosts: tuple[str, ...]
    path_prefixes: tuple[str, ...]
    country: str
    source_category: str
    stage: int
    relevance: tuple[str, ...]
    rationale: str


@dataclass(frozen=True)
class ExtractedSegment:
    text: str
    page_number: int | None = None
    section: str | None = None


@dataclass
class RetrievedDocument:
    source_id: str
    title: str
    organization_name: str
    original_url: str
    final_url: str
    source_category: str
    country: str
    stage: int
    content_type: str | None
    retrieval_status: str
    retrieved_at: str | None
    acceptance_reason: str
    manual_review_required: bool = False
    page_count: int | None = None
    segments: list[ExtractedSegment] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    candidate_links: list[tuple[str, str]] = field(default_factory=list)

    @property
    def source_classification(self) -> str:
        return "Sri Lankan" if self.stage == 1 else "International fallback"

    @property
    def extracted_text(self) -> str:
        blocks = []
        for segment in self.segments:
            marker = []
            if segment.page_number:
                marker.append(f"page={segment.page_number}")
            if segment.section:
                marker.append(f"section={segment.section}")
            prefix = f"[{' | '.join(marker)}]\n" if marker else ""
            blocks.append(prefix + segment.text)
        return "\n\n".join(blocks)


class SourcePolicy:
    def __init__(self, entries: list[SourcePolicyEntry], version: str = "unversioned", content_hash: str = "") -> None:
        self.entries = entries
        self.version = version
        self.content_hash = content_hash

    @classmethod
    def load_default(cls) -> "SourcePolicy":
        path = Path(__file__).resolve().parents[1] / "source_policy" / "crop_finding_sources.json"
        raw = path.read_bytes()
        payload = json.loads(raw.decode("utf-8"))
        return cls([
            SourcePolicyEntry(
                id=item["id"],
                organization_name=item["organizationName"],
                approved_hosts=tuple(host.lower().rstrip(".") for host in item["approvedHosts"]),
                path_prefixes=tuple(item["pathPrefixes"]),
                country=item["country"],
                source_category=item["sourceCategory"],
                stage=int(item["stage"]),
                relevance=tuple(item["relevance"]),
                rationale=item["rationale"],
            )
            for item in payload["sources"]
        ], version=str(payload.get("version") or "unversioned"), content_hash=hashlib.sha256(raw).hexdigest())

    def allowed_hosts(self, stage: int) -> list[str]:
        return sorted({host for entry in self.entries if entry.stage == stage for host in entry.approved_hosts})

    def match_url(self, url: str, expected_stage: int | None = None) -> SourcePolicyEntry:
        parsed = urlsplit(url)
        if parsed.scheme not in {"http", "https"}:
            raise SourcePolicyError("Only HTTP and HTTPS source URLs are allowed.")
        if parsed.username or parsed.password:
            raise SourcePolicyError("Source URLs containing credentials are not allowed.")
        try:
            port = parsed.port
        except ValueError as exc:
            raise SourcePolicyError("Source URL has an invalid port.") from exc
        expected_port = 443 if parsed.scheme == "https" else 80
        if port is not None and port != expected_port:
            raise SourcePolicyError("Source URL port does not match its HTTP or HTTPS scheme.")
        host = (parsed.hostname or "").lower().rstrip(".")
        if not host:
            raise SourcePolicyError("Source URL must contain a host.")
        path = parsed.path or "/"
        matches = [
            (len(prefix), entry)
            for entry in self.entries
            if (expected_stage is None or entry.stage == expected_stage)
            and host in entry.approved_hosts
            for prefix in entry.path_prefixes
            if path.lower().startswith(prefix.lower())
        ]
        if not matches:
            raise SourcePolicyError("Source URL is not in the approved CropFinding source catalog.")
        return max(matches, key=lambda match: match[0])[1]


Resolver = Callable[[str, int | None], Awaitable[set[str]]]


class CropFindingTools:
    def __init__(
        self,
        settings: Settings,
        source_policy: SourcePolicy | None = None,
        transport: httpx.AsyncBaseTransport | None = None,
        resolver: Resolver | None = None,
    ) -> None:
        self.settings = settings
        self.source_policy = source_policy or SourcePolicy.load_default()
        self._transport = transport
        self._resolver = resolver or self._resolve_public_addresses
        self._require_peer_validation = transport is None

    async def retrieve_many(
        self,
        candidates: list[tuple[str, str]],
        stage: int,
        limit: int,
    ) -> tuple[list[RetrievedDocument], list[str]]:
        documents: list[RetrievedDocument] = []
        warnings: list[str] = []
        seen: set[str] = set()
        for url, title in candidates:
            if len(documents) >= limit:
                break
            canonical = self.normalize_url(url)
            if canonical in seen:
                continue
            seen.add(canonical)
            try:
                document = await asyncio.wait_for(
                    self.retrieve(url, title, stage),
                    timeout=self.settings.crop_finding_document_timeout_seconds,
                )
                documents.append(document)
            except (asyncio.TimeoutError, RetrievalError, SourcePolicyError) as exc:
                warnings.append(f"Skipped source {url}: {exc}")
        return documents, warnings

    async def retrieve(self, url: str, discovered_title: str, stage: int) -> RetrievedDocument:
        original_url = url
        current_url = url
        timeout = httpx.Timeout(
            connect=self.settings.crop_finding_connect_timeout_seconds,
            read=self.settings.crop_finding_read_timeout_seconds,
            write=self.settings.crop_finding_read_timeout_seconds,
            pool=self.settings.crop_finding_connect_timeout_seconds,
        )
        async with httpx.AsyncClient(
            timeout=timeout,
            follow_redirects=False,
            trust_env=False,
            transport=self._transport,
            headers={"User-Agent": "AgriAssist-CropFinding/1.0"},
        ) as client:
            for redirect_number in range(self.settings.crop_finding_max_redirects + 1):
                entry = self.source_policy.match_url(current_url, stage)
                parsed_current = urlsplit(current_url)
                resolved = await self._resolver(
                    parsed_current.hostname or "",
                    parsed_current.port or (443 if parsed_current.scheme == "https" else 80),
                )
                async with client.stream("GET", current_url, headers={"Accept": "text/html,application/xhtml+xml,application/pdf"}) as response:
                    self._validate_connected_peer(response, resolved)
                    if response.status_code in {301, 302, 303, 307, 308}:
                        if redirect_number >= self.settings.crop_finding_max_redirects:
                            raise RetrievalError("Source exceeded the configured redirect limit.")
                        location = response.headers.get("location")
                        if not location:
                            raise RetrievalError("Source returned a redirect without a destination.")
                        redirected = urljoin(current_url, location)
                        self.source_policy.match_url(redirected, stage)
                        current_url = redirected
                        continue
                    if response.status_code >= 400:
                        raise RetrievalError(f"Source returned HTTP {response.status_code}.")
                    content_type = response.headers.get("content-type", "").split(";", 1)[0].strip().lower()
                    is_pdf = content_type == "application/pdf" or (
                        content_type == "application/octet-stream" and urlsplit(current_url).path.lower().endswith(".pdf")
                    )
                    is_html = content_type in {"text/html", "application/xhtml+xml"}
                    if not is_pdf and not is_html:
                        raise RetrievalError(f"Unsupported source content type '{content_type or 'unknown'}'.")
                    max_bytes = self.settings.crop_finding_max_pdf_bytes if is_pdf else self.settings.crop_finding_max_html_bytes
                    body = await self._read_bounded(response, max_bytes)
                    final_url = str(response.url)
                    final_entry = self.source_policy.match_url(final_url, stage)
                    if final_entry.id != entry.id and final_entry.stage != entry.stage:
                        raise RetrievalError("Final source destination does not match the approved source policy.")
                    if is_pdf:
                        return self._extract_pdf(original_url, final_url, discovered_title, final_entry, content_type, body)
                    return self._extract_html(original_url, final_url, discovered_title, final_entry, content_type, body, response.encoding)
        raise RetrievalError("Source retrieval ended unexpectedly.")

    async def _read_bounded(self, response: httpx.Response, max_bytes: int) -> bytes:
        chunks: list[bytes] = []
        size = 0
        async for chunk in response.aiter_bytes():
            size += len(chunk)
            if size > max_bytes:
                raise RetrievalError("Source document exceeds the configured size limit.")
            chunks.append(chunk)
        return b"".join(chunks)

    def _extract_html(
        self,
        original_url: str,
        final_url: str,
        discovered_title: str,
        entry: SourcePolicyEntry,
        content_type: str,
        body: bytes,
        encoding: str | None,
    ) -> RetrievedDocument:
        soup = BeautifulSoup(body.decode(encoding or "utf-8", errors="replace"), "html.parser")
        candidate_links: list[tuple[str, str]] = []
        for anchor in soup.find_all("a", href=True):
            target = urljoin(final_url, str(anchor.get("href") or ""))
            title = " ".join(anchor.get_text(" ", strip=True).split())[:240]
            try:
                self.source_policy.match_url(target, entry.stage)
            except SourcePolicyError:
                continue
            candidate_links.append((self.normalize_url(target), title))
        for tag in soup(["script", "style", "noscript", "nav", "footer", "header", "form", "aside"]):
            tag.decompose()
        title = (soup.title.get_text(" ", strip=True) if soup.title else "") or discovered_title or entry.organization_name
        segments: list[ExtractedSegment] = []
        current_heading: str | None = None
        chars = 0
        for node in soup.find_all(["h1", "h2", "h3", "h4", "p", "li", "td", "th"]):
            text = " ".join(node.get_text(" ", strip=True).split())
            if not text:
                continue
            if node.name in {"h1", "h2", "h3", "h4"}:
                current_heading = text[:240]
                continue
            remaining = self.settings.crop_finding_max_extracted_chars_per_source - chars
            if remaining <= 0:
                break
            text = text[:remaining]
            if len(text) < 20:
                continue
            segments.append(ExtractedSegment(text=text, section=current_heading))
            chars += len(text)
        if not segments:
            return self._manual_review_document(
                original_url, final_url, title, entry, content_type,
                "The HTML source contained no reliably extractable readable text.",
            )
        return RetrievedDocument(
            source_id=self._document_id(entry, final_url),
            title=title[:300],
            organization_name=entry.organization_name,
            original_url=original_url,
            final_url=final_url,
            source_category=entry.source_category,
            country=entry.country,
            stage=entry.stage,
            content_type=content_type,
            retrieval_status="Retrieved",
            retrieved_at=datetime.now(timezone.utc).isoformat(),
            acceptance_reason=entry.rationale,
            segments=segments,
            candidate_links=candidate_links,
        )

    def _extract_pdf(
        self,
        original_url: str,
        final_url: str,
        discovered_title: str,
        entry: SourcePolicyEntry,
        content_type: str,
        body: bytes,
    ) -> RetrievedDocument:
        try:
            reader = PdfReader(BytesIO(body), strict=False)
            page_count = len(reader.pages)
            segments: list[ExtractedSegment] = []
            chars = 0
            for page_index, page in enumerate(reader.pages[: self.settings.crop_finding_max_pdf_pages], start=1):
                text = " ".join((page.extract_text() or "").split())
                if len(text) < 20:
                    continue
                remaining = self.settings.crop_finding_max_extracted_chars_per_source - chars
                if remaining <= 0:
                    break
                text = text[:remaining]
                segments.append(ExtractedSegment(text=text, page_number=page_index))
                chars += len(text)
        except Exception as exc:
            return self._manual_review_document(
                original_url, final_url, discovered_title or entry.organization_name, entry, content_type,
                f"The PDF could not be parsed reliably ({type(exc).__name__}).",
            )
        if not segments:
            document = self._manual_review_document(
                original_url, final_url, discovered_title or entry.organization_name, entry, content_type,
                "The PDF appears scanned or image-only; OCR is outside the MVP.",
            )
            document.page_count = page_count
            return document
        return RetrievedDocument(
            source_id=self._document_id(entry, final_url),
            title=(discovered_title or entry.organization_name)[:300],
            organization_name=entry.organization_name,
            original_url=original_url,
            final_url=final_url,
            source_category=entry.source_category,
            country=entry.country,
            stage=entry.stage,
            content_type=content_type,
            retrieval_status="Retrieved",
            retrieved_at=datetime.now(timezone.utc).isoformat(),
            acceptance_reason=entry.rationale,
            page_count=page_count,
            segments=segments,
            warnings=["Only the configured maximum number of pages was extracted."] if page_count > self.settings.crop_finding_max_pdf_pages else [],
        )

    def _manual_review_document(
        self,
        original_url: str,
        final_url: str,
        title: str,
        entry: SourcePolicyEntry,
        content_type: str,
        warning: str,
    ) -> RetrievedDocument:
        return RetrievedDocument(
            source_id=self._document_id(entry, final_url),
            title=title[:300],
            organization_name=entry.organization_name,
            original_url=original_url,
            final_url=final_url,
            source_category=entry.source_category,
            country=entry.country,
            stage=entry.stage,
            content_type=content_type,
            retrieval_status="Manual Review Required",
            retrieved_at=datetime.now(timezone.utc).isoformat(),
            acceptance_reason=entry.rationale,
            manual_review_required=True,
            warnings=[warning],
        )

    def _validate_connected_peer(self, response: httpx.Response, resolved_addresses: set[str]) -> None:
        network_stream = response.extensions.get("network_stream")
        if network_stream is None:
            if self._require_peer_validation:
                raise RetrievalError("Could not verify the connected source address.")
            return
        peer = network_stream.get_extra_info("server_addr") or network_stream.get_extra_info("peername")
        if not peer:
            raise RetrievalError("Could not verify the connected source address.")
        peer_ip = str(peer[0] if isinstance(peer, (tuple, list)) else peer)
        self._ensure_public_ip(peer_ip)
        if resolved_addresses and peer_ip not in resolved_addresses:
            raise RetrievalError("Connected source address changed after DNS validation.")

    @staticmethod
    async def _resolve_public_addresses(host: str, port: int | None) -> set[str]:
        try:
            infos = await asyncio.get_running_loop().run_in_executor(
                None,
                lambda: socket.getaddrinfo(host, port or 443, type=socket.SOCK_STREAM),
            )
        except socket.gaierror as exc:
            raise RetrievalError("Source host could not be resolved.") from exc
        addresses = {info[4][0] for info in infos}
        if not addresses:
            raise RetrievalError("Source host resolved to no addresses.")
        for address in addresses:
            CropFindingTools._ensure_public_ip(address)
        return addresses

    @staticmethod
    def _ensure_public_ip(address: str) -> None:
        try:
            ip = ipaddress.ip_address(address)
        except ValueError as exc:
            raise RetrievalError("Source resolved to an invalid address.") from exc
        if (
            not ip.is_global
            or ip.is_private
            or ip.is_loopback
            or ip.is_link_local
            or ip.is_multicast
            or ip.is_reserved
            or ip.is_unspecified
        ):
            raise RetrievalError("Source resolved or connected to a non-public address.")

    @staticmethod
    def normalize_url(url: str) -> str:
        parsed = urlsplit(url)
        host = (parsed.hostname or "").lower().rstrip(".")
        default_port = parsed.port in {None, 80 if parsed.scheme == "http" else 443}
        authority = host if default_port else f"{host}:{parsed.port}"
        path = parsed.path or "/"
        if path != "/":
            path = path.rstrip("/")
        query = "&".join(
            part for part in parsed.query.split("&")
            if part and not part.lower().startswith(("utm_", "fbclid=", "gclid="))
        )
        return urlunsplit((parsed.scheme.lower(), authority, path, query, ""))

    @classmethod
    def _document_id(cls, entry: SourcePolicyEntry, url: str) -> str:
        digest = hashlib.sha256(cls.normalize_url(url).encode("utf-8")).hexdigest()[:12]
        return f"{entry.id}:{digest}"
