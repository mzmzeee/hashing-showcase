from __future__ import annotations

import json
import os
import subprocess
import tempfile
from pathlib import Path
from typing import Any

from flask import Flask, Response, jsonify, request, send_file

app = Flask(__name__)


def _validate_payload(payload: dict[str, Any]) -> tuple[bool, list[str]]:
    required_fields = (
        "message",
        "message_hash_hex",
    )
    errors: list[str] = []
    for key in required_fields:
        value = payload.get(key)
        if not isinstance(value, str) or not value.strip():
            errors.append(f"Missing or invalid field: {key}")

    # Optional fields with validation
    signature_base64 = payload.get("signature_base64")
    if signature_base64 is not None and not isinstance(signature_base64, str):
        errors.append("signature_base64 must be a string when provided.")

    hashes_match = payload.get("hashes_match")
    if hashes_match is not None and not isinstance(hashes_match, bool):
        errors.append("hashes_match must be a boolean when provided.")

    verification_status = payload.get("verification_status")
    if verification_status is not None and verification_status not in ("Valid", "Invalid", "Unsigned"):
        errors.append("verification_status must be one of: Valid, Invalid, Unsigned")

    return (len(errors) == 0, errors)


def _run_manim(payload_path: Path, media_dir: Path) -> Path:
    env = os.environ.copy()
    env["ANIMATION_DATA_PATH"] = str(payload_path)

    cmd = [
        "manim",
        "-ql",
        "scene.py",
        "SignatureVisualization",
        "--media_dir",
        str(media_dir),
        "--output_file",
        "signature",
    ]

    # Reduced timeout for faster test execution (can be increased for production)
    timeout_seconds = 60 if os.environ.get("FLASK_ENV") == "test" else 180
    result = subprocess.run(
        cmd,
        cwd=Path(__file__).parent,
        env=env,
        capture_output=True,
        text=True,
        timeout=timeout_seconds,
        check=False,
    )

    if result.returncode != 0:
        raise RuntimeError(
            "Manim rendering failed",
            {
                "returncode": result.returncode,
                "stdout": result.stdout,
                "stderr": result.stderr,
            },
        )

    videos = sorted(media_dir.rglob("*.mp4"))
    if not videos:
        raise FileNotFoundError("Rendered video not found.")

    return videos[0]


@app.post("/generate-animation")
def generate_animation() -> Response:
    if not request.is_json:
        return jsonify({"error": "Request body must be JSON."}), 400

    payload = request.get_json()  # type: ignore[assignment]
    if not isinstance(payload, dict):
        return jsonify({"error": "Invalid JSON payload."}), 400

    is_valid, errors = _validate_payload(payload)
    if not is_valid:
        return jsonify({"error": "Invalid payload.", "details": errors}), 400

    with tempfile.TemporaryDirectory() as tmp_dir:
        tmp_path = Path(tmp_dir)
        media_dir = tmp_path / "media"
        media_dir.mkdir(parents=True, exist_ok=True)

        payload_path = tmp_path / "payload.json"
        payload_path.write_text(json.dumps(payload), encoding="utf-8")

        try:
            video_path = _run_manim(payload_path, media_dir)
        except subprocess.TimeoutExpired:
            return jsonify({"error": "Manim rendering timed out."}), 504
        except RuntimeError as exc:
            _, details = exc.args
            return jsonify({"error": "Manim rendering failed.", "details": details}), 500
        except FileNotFoundError as exc:
            return jsonify({"error": str(exc)}), 500

        return send_file(video_path, mimetype="video/mp4", as_attachment=False)


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)
