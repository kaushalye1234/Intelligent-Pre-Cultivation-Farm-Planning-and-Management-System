import asyncio
import hashlib
import re
import time
from dataclasses import dataclass
from urllib.parse import urlsplit

from config import Settings
from schemas.inspection_image_analysis import (
    EvidenceRelevanceSignals,
    GroundingEvidence,
    InspectionImageAnalysisInput,
    InspectionImagePass1Result,
)
from tools.crop_finding_tools import (
    CropFindingTools,
    RetrievedDocument,
    RetrievalError,
    SourcePolicy,
    SourcePolicyEntry,
    SourcePolicyError,
)


RELEVANCE_RULE_VERSION = 1
_WORD = re.compile(r"[a-z0-9]+")
_ISSUE_SYNONYMS: dict[str, tuple[str, ...]] = {
    "Pest": ("pest", "insect damage", "feeding damage", "chewing damage", "boring damage"),
    "Fungal": ("fungal symptoms", "fungal", "mould", "leaf spot symptoms", "leaf spot"),
    "Bacterial": ("bacterial symptoms", "bacterial lesions", "bacterial"),
    "DiseaseLike": ("disease symptoms", "leaf lesions", "crop disease", "plant health"),
    "NutrientStress": ("nutrient deficiency", "nutrient stress", "chlorosis", "yellowing"),
    "EnvironmentalStress": ("drought stress", "heat stress", "water stress", "wilting"),
    "PhysicalDamage": ("mechanical damage", "physical injury", "physical damage"),
    "Other": ("crop health", "plant health", "visible symptoms"),
    "Unknown": ("crop health", "plant health", "visible symptoms"),
}
_CROP_ALIASES: dict[str, tuple[str, ...]] = {
    "rice": ("rice", "paddy"),
    "maize": ("maize", "corn"),
    "coconut": ("coconut",),
    "tea": ("tea",),
    "rubber": ("rubber",),
}
_ACTION_TERMS = (
    "sanitation", "remove affected", "remove infected", "crop residue", "inspect nearby",
    "monitor symptoms", "monitor spread", "further assessment", "seek advice", "extension officer",
)


@dataclass(frozen=True)
class _ScoredDocument:
    evidence: GroundingEvidence
    strong: bool
    covered_actions: frozenset[str]
    covered_issues: frozenset[str]


class CropHealthEvidenceAdapter:
    """Deterministic Member 2 adapter over Member 1 source policy/retrieval primitives."""

    def __init__(
        self,
        settings: Settings,
        tools: CropFindingTools | None = None,
        source_policy: SourcePolicy | None = None,
    ) -> None:
        self.settings = settings
        self.source_policy = source_policy or SourcePolicy.load_default()
        self.tools = tools or CropFindingTools(settings, source_policy=self.source_policy)

    async def retrieve(
        self,
        request: InspectionImageAnalysisInput,
        pass1: InspectionImagePass1Result,
    ) -> list[GroundingEvidence]:
        deadline = time.monotonic() + self.settings.inspection_image_retrieval_timeout_seconds
        crop_terms = self._crop_terms(request.crop_name)
        issue_terms, primary_terms = self._issue_terms(pass1)
        selected = await self._retrieve_stage(1, request, crop_terms, issue_terms, primary_terms, deadline)
        if not selected:
            selected = await self._retrieve_stage(2, request, crop_terms, issue_terms, primary_terms, deadline)
        return self._select_novel_packet(selected)

    async def _retrieve_stage(
        self,
        stage: int,
        request: InspectionImageAnalysisInput,
        crop_terms: tuple[str, ...],
        issue_terms: tuple[str, ...],
        primary_terms: tuple[str, ...],
        deadline: float,
    ) -> list[_ScoredDocument]:
        entry_limit = self.settings.inspection_image_stage1_entry_limit if stage == 1 else self.settings.inspection_image_stage2_entry_limit
        document_limit = self.settings.inspection_image_stage1_document_limit if stage == 1 else self.settings.inspection_image_stage2_document_limit
        entries = sorted(
            (entry for entry in self.source_policy.entries if entry.stage == stage),
            key=lambda entry: (-self._entry_rank(entry, crop_terms, issue_terms), entry.id),
        )[:entry_limit]
        usable: list[_ScoredDocument] = []
        fetched = 0
        seen: set[str] = set()
        for entry in entries:
            if fetched >= document_limit or time.monotonic() >= deadline:
                break
            entry_url = self._entry_url(entry)
            document = await self._retrieve_one(entry_url, entry.organization_name, stage, deadline)
            if document is None:
                continue
            fetched += 1
            seen.add(self.tools.normalize_url(document.final_url))
            scored = self._score_document(document, crop_terms, issue_terms, primary_terms)
            if scored is not None:
                usable.append(scored)
                if scored.strong:
                    break
            links = sorted(
                document.candidate_links[: self.settings.inspection_image_candidate_links_per_page],
                key=lambda pair: (-self._candidate_rank(pair[0], pair[1], crop_terms, issue_terms), pair[0]),
            )
            for url, title in links:
                if fetched >= document_limit or time.monotonic() >= deadline:
                    break
                normalized = self.tools.normalize_url(url)
                if normalized in seen:
                    continue
                seen.add(normalized)
                child = await self._retrieve_one(url, title, stage, deadline)
                if child is None:
                    continue
                fetched += 1
                scored = self._score_document(child, crop_terms, issue_terms, primary_terms)
                if scored is not None:
                    usable.append(scored)
                    if scored.strong:
                        return usable
        return usable

    async def _retrieve_one(self, url: str, title: str, stage: int, deadline: float) -> RetrievedDocument | None:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return None
        try:
            return await asyncio.wait_for(
                self.tools.retrieve(url, title, stage),
                timeout=min(self.settings.crop_finding_document_timeout_seconds, remaining),
            )
        except (asyncio.TimeoutError, RetrievalError, SourcePolicyError):
            return None

    def _score_document(
        self,
        document: RetrievedDocument,
        crop_terms: tuple[str, ...],
        issue_terms: tuple[str, ...],
        primary_terms: tuple[str, ...],
    ) -> _ScoredDocument | None:
        if document.retrieval_status != "Retrieved" or document.manual_review_required:
            return None
        body = self._normalize(document.extracted_text)
        if len(body) < self.settings.inspection_image_min_readable_chars:
            return None
        title = self._normalize(document.title)
        url = self._normalize(urlsplit(document.final_url).path)
        headings = self._normalize(" ".join(segment.section or "" for segment in document.segments))
        crop_match = self._contains_any(" ".join((url, title, headings, body)), crop_terms)
        issue_match = self._contains_any(" ".join((url, title, headings, body)), issue_terms)
        if not crop_match or not issue_match:
            return None

        url_score = self._signal_score(url, crop_terms, issue_terms, 6)
        title_score = self._signal_score(title, crop_terms, issue_terms, 10)
        heading_score = self._signal_score(headings, crop_terms, issue_terms, 8)
        body_score = self._signal_score(body, crop_terms, issue_terms, 2)
        total = url_score + title_score + heading_score + body_score
        if total < self.settings.inspection_image_relevance_threshold:
            return None

        extract = self._extract_snippet(document, crop_terms, issue_terms)
        if not extract:
            return None
        actions = frozenset(term for term in _ACTION_TERMS if term in body)
        covered_issues = frozenset(term for term in issue_terms if term in " ".join((title, headings, body)))
        strong_crop = self._contains_any(" ".join((url, title, headings)), crop_terms)
        strong_primary = self._contains_any(" ".join((title, headings)), primary_terms)
        strong = (
            total >= self.settings.inspection_image_strong_evidence_threshold
            and strong_crop
            and strong_primary
            and bool(actions)
        )
        entry = self.source_policy.match_url(document.final_url, document.stage)
        normalized_full = self._normalize(document.extracted_text)
        evidence = GroundingEvidence(
            sourcePolicyId=entry.id,
            sourceStage="Stage1" if document.stage == 1 else "Stage2",
            organization=document.organization_name,
            sourceCategory=document.source_category,
            finalUrl=document.final_url,
            documentTitle=document.title,
            retrievedAt=document.retrieved_at or "unknown",
            normalizedDocumentSha256=hashlib.sha256(normalized_full.encode("utf-8")).hexdigest(),
            relevanceSignals=EvidenceRelevanceSignals(
                totalScore=total,
                cropMatch=True,
                issueMatch=True,
                titleScore=title_score,
                headingScore=heading_score,
                urlScore=url_score,
                bodyScore=body_score,
                strongEvidence=strong,
            ),
            relevanceRuleVersion=RELEVANCE_RULE_VERSION,
            exactExtract=extract,
        )
        return _ScoredDocument(evidence, strong, actions, covered_issues)

    def _extract_snippet(self, document: RetrievedDocument, crop_terms: tuple[str, ...], issue_terms: tuple[str, ...]) -> str:
        candidates: list[tuple[int, str]] = []
        for segment in document.segments:
            text = self._normalize(segment.text)
            heading = self._normalize(segment.section or "")
            combined = f"{heading} {text}".strip()
            score = self._signal_score(combined, crop_terms, issue_terms, 4)
            if self._contains_any(combined, issue_terms) or self._contains_any(combined, _ACTION_TERMS):
                rendered = f"{segment.section}: {text}" if segment.section else text
                candidates.append((score, rendered))
        candidates.sort(key=lambda item: (-item[0], item[1]))
        selected: list[str] = []
        local_seen: set[str] = set()
        target = min(self.settings.inspection_image_evidence_chars_per_source, 3000)
        for _, text in candidates:
            normalized = self._normalize(text)
            if normalized in local_seen:
                continue
            local_seen.add(normalized)
            remaining = target - sum(len(item) for item in selected) - max(0, len(selected) - 1) * 2
            if remaining <= 0:
                break
            selected.append(text[:remaining])
        return "\n\n".join(selected).strip()

    def _select_novel_packet(self, documents: list[_ScoredDocument]) -> list[GroundingEvidence]:
        selected: list[GroundingEvidence] = []
        total_chars = 0
        covered_actions: set[str] = set()
        covered_issues: set[str] = set()
        for scored in sorted(documents, key=lambda item: (-item.evidence.relevance_signals.total_score, item.evidence.final_url)):
            extract = scored.evidence.exact_extract
            if any(self._similarity(extract, existing.exact_extract) >= self.settings.inspection_image_duplicate_similarity for existing in selected):
                continue
            if selected and not (set(scored.covered_actions) - covered_actions or set(scored.covered_issues) - covered_issues):
                continue
            remaining = self.settings.inspection_image_evidence_total_chars - total_chars
            if remaining <= 0 or len(selected) >= self.settings.inspection_image_evidence_document_limit:
                break
            if len(extract) > remaining:
                scored.evidence.exact_extract = extract[:remaining]
            selected.append(scored.evidence)
            total_chars += len(scored.evidence.exact_extract)
            covered_actions.update(scored.covered_actions)
            covered_issues.update(scored.covered_issues)
            if scored.strong:
                break
        return selected

    @staticmethod
    def _entry_url(entry: SourcePolicyEntry) -> str:
        host = sorted(entry.approved_hosts, key=lambda value: (value.startswith("www."), value))[0]
        path = sorted(entry.path_prefixes, key=lambda value: (-len(value), value))[0] or "/"
        return f"https://{host}{path}"

    @staticmethod
    def _crop_terms(crop_name: str) -> tuple[str, ...]:
        normalized = CropHealthEvidenceAdapter._normalize(crop_name)
        return tuple(dict.fromkeys((normalized, *_CROP_ALIASES.get(normalized, ()))))

    @staticmethod
    def _issue_terms(pass1: InspectionImagePass1Result) -> tuple[tuple[str, ...], tuple[str, ...]]:
        categories = [intent.issue_category for intent in pass1.search_intents] or list(pass1.possible_issue_categories)
        all_terms = tuple(dict.fromkeys(term for category in categories for term in _ISSUE_SYNONYMS[category]))
        primary = next((intent.issue_category for intent in pass1.search_intents if intent.is_primary), categories[0] if categories else "Unknown")
        return all_terms, _ISSUE_SYNONYMS[primary]

    @staticmethod
    def _entry_rank(entry: SourcePolicyEntry, crop_terms: tuple[str, ...], issue_terms: tuple[str, ...]) -> int:
        text = CropHealthEvidenceAdapter._normalize(" ".join((entry.id, *entry.relevance, entry.rationale)))
        return sum(4 for term in crop_terms if term in text) + sum(2 for term in issue_terms if term in text) + (2 if "plant protection" in text else 0)

    @staticmethod
    def _candidate_rank(url: str, title: str, crop_terms: tuple[str, ...], issue_terms: tuple[str, ...]) -> int:
        return CropHealthEvidenceAdapter._signal_score(
            CropHealthEvidenceAdapter._normalize(f"{url} {title}"), crop_terms, issue_terms, 3
        )

    @staticmethod
    def _signal_score(text: str, crop_terms: tuple[str, ...], issue_terms: tuple[str, ...], weight: int) -> int:
        return weight * (int(CropHealthEvidenceAdapter._contains_any(text, crop_terms)) + int(CropHealthEvidenceAdapter._contains_any(text, issue_terms)))

    @staticmethod
    def _contains_any(text: str, terms: tuple[str, ...]) -> bool:
        return any(term and term in text for term in terms)

    @staticmethod
    def _normalize(value: str) -> str:
        return " ".join(_WORD.findall(value.casefold()))

    @staticmethod
    def _similarity(left: str, right: str) -> float:
        def shingles(value: str) -> set[tuple[str, ...]]:
            tokens = _WORD.findall(value.casefold())
            if len(tokens) < 5:
                return {tuple(tokens)} if tokens else set()
            return {tuple(tokens[index:index + 5]) for index in range(len(tokens) - 4)}
        left_set, right_set = shingles(left), shingles(right)
        if not left_set or not right_set:
            return 0.0
        return len(left_set & right_set) / len(left_set | right_set)
