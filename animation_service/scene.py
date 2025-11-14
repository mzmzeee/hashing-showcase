from __future__ import annotations

import json
import os
import textwrap
from pathlib import Path

from manim import (
    DOWN,
    UP,
    LEFT,
    RIGHT,
    FadeIn,
    FadeOut,
    Create,
    Write,
    Scene,
    Text,
    VGroup,
    Rectangle,
    Arrow,
)


def _load_request_payload() -> dict[str, str]:
    data_path = os.environ.get("ANIMATION_DATA_PATH")
    if not data_path:
        raise RuntimeError("ANIMATION_DATA_PATH environment variable is not set.")
    with Path(data_path).open("r", encoding="utf-8") as handle:
        return json.load(handle)
def _wrap(text: str, width: int = 60) -> str:
    if not text:
        return text
    return "\n".join(textwrap.wrap(text, width=width))


class SignatureVisualization(Scene):
    """Visualize hashing and RSA signing using the provided payload."""

    def construct(self) -> None:  # noqa: D401 - manim entry point
        data = _load_request_payload()
        message = data["message"]
        message_hash_hex = data["message_hash_hex"]
        signature_b64 = data["signature_base64"]
        decrypted_hash_hex = data["decrypted_hash_hex"]
        recomputed_hash_hex = data.get("recomputed_hash_hex", message_hash_hex)
        verification_status = data.get("verification_status", "Unsigned")
        hashes_match = data.get("hashes_match")
        if hashes_match is None:
            hashes_match = decrypted_hash_hex == recomputed_hash_hex

        # Determine title and color based on verification status
        if verification_status == "Valid":
            title_text = "Digital Signature Journey - Valid"
            title_color = "#2E7D32"  # Green
        elif verification_status == "Invalid":
            title_text = "Digital Signature Journey - Invalid"
            title_color = "#C62828"  # Red
        else:  # Unsigned
            title_text = "Digital Signature Journey - Unsigned"
            title_color = "#FF6F00"  # Orange

        title = Text(title_text, font_size=44, color=title_color)
        title.to_edge(UP)
        self.play(FadeIn(title, shift=DOWN * 0.5))
        self.wait(1)

        # Build steps based on verification status
        steps = [
            self._create_step("1. Plain Message", _wrap(message), "#424242"),
            self._create_step("2. Hash with SHA-256", _wrap(message_hash_hex), "#283593"),
        ]

        if verification_status == "Unsigned":
            # For unsigned messages, show that no signature was provided
            steps.append(self._create_step("3. Signature Status", "No signature provided - Message is unsigned", "#FF6F00"))
            steps.append(self._create_step("4. Result", "This message cannot be verified", "#FF6F00"))
        else:
            # For signed messages, show the full verification process
            steps.append(self._create_step("3. Sign Hash (Private Key)", _wrap(signature_b64), "#6A1B9A"))
            steps.append(self._create_step("4. Verify Signature (Public Key)", _wrap(decrypted_hash_hex), "#00838F"))
            steps.append(self._create_step(
                "5. Recompute Hash",
                _wrap(recomputed_hash_hex),
                "#1565C0",
            ))
            steps.append(self._create_step(
                "6. Compare",
                "Hashes Match: {result}".format(result="Yes" if hashes_match else "No"),
                "#2E7D32" if hashes_match else "#C62828",
            ))
            # Add final result step
            if verification_status == "Valid":
                steps.append(self._create_step("7. Verification Result", "✓ SIGNATURE IS VALID", "#2E7D32"))
            elif verification_status == "Invalid":
                steps.append(self._create_step("7. Verification Result", "✗ SIGNATURE IS INVALID", "#C62828"))

        layout = VGroup(*steps).arrange(DOWN, buff=0.9, aligned_edge=LEFT)
        layout.next_to(title, DOWN, buff=0.7)

        arrows = []
        for upper, lower in zip(steps, steps[1:]):
            arrow = Arrow(
                upper.get_bottom() + DOWN * 0.05,
                lower.get_top() + UP * 0.05,
                buff=0.2,
                stroke_width=3,
                color="#757575",
            )
            arrows.append(arrow)

        for box in steps:
            self.play(FadeIn(box, shift=RIGHT * 0.3), run_time=0.8)
            self.wait(0.6)

        for arrow in arrows:
            self.play(Create(arrow), run_time=0.5)
            self.wait(0.4)

        # Final text based on verification status
        if verification_status == "Valid":
            final_text = Text(
                "Signature Verification Complete - Valid",
                font_size=36,
                color="#43A047",
            )
        elif verification_status == "Invalid":
            final_text = Text(
                "Signature Verification Complete - Invalid",
                font_size=36,
                color="#E53935",
            )
        else:  # Unsigned
            final_text = Text(
                "Message is Unsigned - Cannot Verify",
                font_size=36,
                color="#FF6F00",
            )
        final_text.next_to(steps[-1], DOWN, buff=0.8)
        self.play(Write(final_text))
        self.wait(2)

        self.play(FadeOut(VGroup(layout, *arrows, final_text, title), shift=DOWN * 0.5))
        self.wait(0.5)

    def _create_step(self, title: str, body: str, color: str) -> VGroup:
        header = Text(title, font_size=28, color=color)
        description = Text(body if body else "(empty)", font_size=24)
        description.next_to(header, DOWN, aligned_edge=LEFT)

        box_padding = 0.5
        rect_width = max(header.width, description.width) + box_padding
        rect_height = header.height + description.height + box_padding
        backdrop = Rectangle(width=rect_width, height=rect_height)
        backdrop.set_stroke(color=color, width=2).set_fill(color=color, opacity=0.08)
        group = VGroup(backdrop, header, description)
        header.move_to(backdrop.get_top() + DOWN * (header.height / 2 + 0.1))
        description.next_to(header, DOWN, buff=0.2, aligned_edge=LEFT)
        return group
