import io
import warnings
from dataclasses import dataclass

from PIL import Image, ImageOps, UnidentifiedImageError

from schemas.inspection_image_analysis import IMAGE_PREPROCESSING_VERSION


class ImagePreprocessingError(RuntimeError):
    pass


@dataclass(frozen=True)
class PreprocessedImage:
    image_bytes: bytes
    mime_type: str
    original_width: int
    original_height: int
    normalized_width: int
    normalized_height: int


class InspectionImagePreprocessor:
    MAX_WIDTH = 12_000
    MAX_HEIGHT = 12_000
    MAX_PIXELS = 40_000_000
    MAX_LONG_EDGE = 1_600
    MAX_DERIVATIVE_BYTES = 3 * 1024 * 1024
    OUTPUT_MIME_TYPE = "image/jpeg"
    _FORMAT_MIME = {"JPEG": "image/jpeg", "PNG": "image/png", "WEBP": "image/webp"}

    @classmethod
    def preprocess(cls, source: bytes, declared_mime_type: str, expected_version: int) -> PreprocessedImage:
        if expected_version != IMAGE_PREPROCESSING_VERSION:
            raise ImagePreprocessingError("image_preprocessing_version_mismatch")
        try:
            Image.MAX_IMAGE_PIXELS = cls.MAX_PIXELS
            with warnings.catch_warnings():
                warnings.simplefilter("error", Image.DecompressionBombWarning)
                with Image.open(io.BytesIO(source)) as opened:
                    opened.load()
                    if getattr(opened, "n_frames", 1) != 1:
                        raise ImagePreprocessingError("animated_image_not_supported")
                    detected_mime = cls._FORMAT_MIME.get(opened.format or "")
                    if detected_mime is None or detected_mime != declared_mime_type:
                        raise ImagePreprocessingError("image_format_mismatch")
                    original_width, original_height = opened.size
                    if (
                        original_width <= 0 or original_height <= 0
                        or original_width > cls.MAX_WIDTH or original_height > cls.MAX_HEIGHT
                        or original_width * original_height > cls.MAX_PIXELS
                    ):
                        raise ImagePreprocessingError("unsafe_image_dimensions")
                    normalized = ImageOps.exif_transpose(opened)
                    normalized.thumbnail((cls.MAX_LONG_EDGE, cls.MAX_LONG_EDGE), Image.Resampling.LANCZOS)
                    if normalized.mode != "RGB":
                        if "A" in normalized.getbands():
                            background = Image.new("RGBA", normalized.size, "white")
                            background.alpha_composite(normalized.convert("RGBA"))
                            normalized = background.convert("RGB")
                        else:
                            normalized = normalized.convert("RGB")
                    clean = Image.new("RGB", normalized.size)
                    clean.paste(normalized)
                    output = io.BytesIO()
                    clean.save(output, format="JPEG", quality=88, optimize=True)
                    derivative = output.getvalue()
                    if len(derivative) > cls.MAX_DERIVATIVE_BYTES:
                        raise ImagePreprocessingError("normalized_image_too_large")
                    return PreprocessedImage(
                        derivative,
                        cls.OUTPUT_MIME_TYPE,
                        original_width,
                        original_height,
                        clean.width,
                        clean.height,
                    )
        except ImagePreprocessingError:
            raise
        except (UnidentifiedImageError, OSError, ValueError, Image.DecompressionBombError, Image.DecompressionBombWarning) as exc:
            raise ImagePreprocessingError("image_preprocessing_failed") from exc
