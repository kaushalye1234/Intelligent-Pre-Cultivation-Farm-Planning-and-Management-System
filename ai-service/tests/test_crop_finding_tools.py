from types import SimpleNamespace

import httpx
import pytest

from config import Settings
from tools.crop_finding_tools import CropFindingTools, RetrievalError, SourcePolicy, SourcePolicyError


async def public_resolver(host: str, port: int | None) -> set[str]:
    return {"93.184.216.34"}


def settings(**overrides):
    return Settings(
        _env_file=None,
        OPENAI_API_KEY="test",
        CROP_FINDING_MODEL="gpt-4.1-mini",
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
async def test_unreadable_pdf_is_retained_for_manual_review():
    async def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, request=request, headers={"content-type": "application/pdf"}, content=b"not-a-pdf")

    tools = CropFindingTools(settings(), transport=httpx.MockTransport(handler), resolver=public_resolver)
    document = await tools.retrieve("https://doa.gov.lk/rrdi/manual.pdf", "Rice manual", 1)

    assert document.retrieval_status == "Manual Review Required"
    assert document.manual_review_required is True
    assert document.original_url == "https://doa.gov.lk/rrdi/manual.pdf"
    assert document.segments == []


def test_connected_peer_must_be_public_and_match_resolved_address():
    stream = SimpleNamespace(get_extra_info=lambda key: ("127.0.0.1", 443) if key == "server_addr" else None)
    response = SimpleNamespace(extensions={"network_stream": stream})
    tools = CropFindingTools(settings())

    with pytest.raises(RetrievalError):
        tools._validate_connected_peer(response, {"93.184.216.34"})
