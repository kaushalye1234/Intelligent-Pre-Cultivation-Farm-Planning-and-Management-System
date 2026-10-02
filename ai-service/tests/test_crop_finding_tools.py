from types import SimpleNamespace

import httpx
import pytest

from config import Settings
import tools.crop_finding_tools as crop_finding_tools_module
from tools.crop_finding_tools import CropFindingTools, RetrievalError, SourcePolicy, SourcePolicyError


async def public_resolver(host: str, port: int | None) -> set[str]:
    return {"93.184.216.34"}


def settings(**overrides):
    return Settings(
        _env_file=None,
        AI_PROVIDER="openai",
        AI_MODEL="gpt-6-luna",
        OPENAI_API_KEY="test",
        **overrides,
    )


def test_source_policy_requires_exact_hosts_and_path_prefixes():
    policy = SourcePolicy.load_default()

    assert policy.match_url("https://doa.gov.lk/hordi-home/", 1).id == "lk-doa-hordi"
    assert policy.match_url("https://www.sab.ac.lk/agri/research", 1).id == "lk-sabaragamuwa-agriculture"
    with pytest.raises(SourcePolicyError):
        policy.match_url("https://blog.example/crops", 1)
    with pytest.raises(SourcePolicyError):
        policy.match_url("https://www.sab.ac.lk/medicine", 1)
    with pytest.raises(SourcePolicyError):
        policy.match_url("https://scs.doa.gov.lk/", 1)
    with pytest.raises(SourcePolicyError):
        policy.match_url("https://doa.gov.lk:8443/hordi-home/", 1)
    with pytest.raises(SourcePolicyError):
        policy.match_url("https://doa.gov.lk:80/hordi-home/", 1)
    with pytest.raises(SourcePolicyError):
        policy.match_url("http://doa.gov.lk:443/hordi-home/", 1)
    with pytest.raises(SourcePolicyError):
        policy.match_url("https://user:secret@doa.gov.lk/hordi-home/", 1)


def test_source_policy_approves_only_the_configured_repository_service_hosts_and_paths():
    policy = SourcePolicy.load_default()

    assert policy.match_url("https://dl.nsf.gov.lk/dl/api/core/bitstreams/example/content", 1).id == "lk-nsf"
    assert policy.match_url("https://dl-doa.nsf.gov.lk/doa/api/core/bitstreams/example/content", 1).id == "lk-doa-repository"
    assert policy.match_url("https://agris.fao.org/search/en/providers/122193/records/example", 2).id == "intl-fao-agris"
    assert policy.match_url("https://glis.fao.org/glis/doi/10.18730/EXAMPLE", 2).id == "intl-fao-glis"

    rejected = [
        ("https://ecocrop.apps.fao.org/ecocrop/srv/en/cropView?id=618", 2),
        ("https://random.fao.org/search/crops", 2),
        ("https://fake.dl-doa.nsf.gov.lk/doa/", 1),
        ("https://agris.fao.org.example.com/search/crops", 2),
        ("https://agris.fao.org/browse/crops", 2),
        ("https://glis.fao.org/about", 2),
        ("https://agris.fao.org/search/crops", 1),
        ("https://dl-doa.nsf.gov.lk/doa/", 2),
    ]
    for url, stage in rejected:
        with pytest.raises(SourcePolicyError):
            policy.match_url(url, stage)


@pytest.mark.parametrize("address", [
    "127.0.0.1", "10.0.0.1", "169.254.169.254", "0.0.0.0", "224.0.0.1", "192.0.2.1", "::1", "fc00::1"
])
def test_ssrf_guard_rejects_non_public_addresses(address):
    with pytest.raises(RetrievalError):
        CropFindingTools._ensure_public_ip(address)


@pytest.mark.asyncio
async def test_html_retrieval_extracts_sections_and_removes_boilerplate():
    resolved_ports: list[int | None] = []

    async def recording_resolver(host: str, port: int | None) -> set[str]:
        resolved_ports.append(port)
        return await public_resolver(host, port)

    async def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200,
            request=request,
            headers={"content-type": "text/html; charset=utf-8"},
            text="""<html><head><title>Rice guide</title></head><body>
            <nav>Ignore navigation</nav><h1>Bg 352</h1>
            <p>Bg 352 is described in this official cultivation reference for Sri Lankan rice farmers.</p>
            <script>ignore()</script></body></html>""",
        )

    tools = CropFindingTools(settings(), transport=httpx.MockTransport(handler), resolver=recording_resolver)
    document = await tools.retrieve("https://doa.gov.lk/rrdi_homepage/bg-352", "", 1)

    assert document.retrieval_status == "Retrieved"
    assert document.organization_name == "Rice Research and Development Institute"
    assert document.title == "Rice guide"
    assert document.segments[0].section == "Bg 352"
    assert "Ignore navigation" not in document.extracted_text
    assert resolved_ports == [443]


@pytest.mark.asyncio
async def test_redirect_to_unapproved_host_is_rejected():
    async def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(302, request=request, headers={"location": "https://example.com/trap"})

    tools = CropFindingTools(settings(), transport=httpx.MockTransport(handler), resolver=public_resolver)
    with pytest.raises(SourcePolicyError):
        await tools.retrieve("https://doa.gov.lk/hordi-home/", "HORDI", 1)


@pytest.mark.asyncio
async def test_redirect_within_approved_agris_path_is_accepted():
    async def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/search/old":
            return httpx.Response(302, request=request, headers={"location": "/search/en/records/1"})
        return httpx.Response(
            200,
            request=request,
            headers={"content-type": "text/html; charset=utf-8"},
            text=(
                "<html><head><title>AGRIS crop record</title></head><body>"
                "<h1>Crop record</h1><p>This agricultural record contains enough readable text for extraction.</p>"
                "</body></html>"
            ),
        )

    tools = CropFindingTools(settings(), transport=httpx.MockTransport(handler), resolver=public_resolver)
    document = await tools.retrieve("https://agris.fao.org/search/old", "AGRIS record", 2)

    assert document.retrieval_status == "Retrieved"
    assert document.source_id.startswith("intl-fao-agris:")
    assert document.final_url == "https://agris.fao.org/search/en/records/1"


@pytest.mark.asyncio
async def test_unreadable_pdf_is_retained_for_manual_review():
    async def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, request=request, headers={"content-type": "application/pdf"}, content=b"not-a-pdf")

    tools = CropFindingTools(settings(), transport=httpx.MockTransport(handler), resolver=public_resolver)
    document = await tools.retrieve("https://doa.gov.lk/rrdi/manual.pdf", "Rice manual", 1)

    assert document.retrieval_status == "Manual Review Required"
    assert document.manual_review_required is True
    assert document.original_url == "https://doa.gov.lk/rrdi/manual.pdf"
    assert document.segments == []


@pytest.mark.asyncio
async def test_text_pdf_preserves_page_provenance(monkeypatch):
    class FakePage:
        def __init__(self, text: str):
            self._text = text

        def extract_text(self):
            return self._text

    class FakeReader:
        pages = [FakePage("Bg 352 has a documented vegetative stage duration in this official rice guide.")]

    monkeypatch.setattr(crop_finding_tools_module, "PdfReader", lambda *args, **kwargs: FakeReader())

    async def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, request=request, headers={"content-type": "application/pdf"}, content=b"pdf")

    tools = CropFindingTools(settings(), transport=httpx.MockTransport(handler), resolver=public_resolver)
    document = await tools.retrieve("https://doa.gov.lk/rrdi/rice-guide.pdf", "Rice guide", 1)

    assert document.retrieval_status == "Retrieved"
    assert document.page_count == 1
    assert document.segments[0].page_number == 1
    assert "vegetative stage" in document.segments[0].text


def test_connected_peer_must_be_public_and_match_resolved_address():
    stream = SimpleNamespace(get_extra_info=lambda key: ("127.0.0.1", 443) if key == "server_addr" else None)
    response = SimpleNamespace(extensions={"network_stream": stream})
    tools = CropFindingTools(settings())

    with pytest.raises(RetrievalError):
        tools._validate_connected_peer(response, {"93.184.216.34"})
