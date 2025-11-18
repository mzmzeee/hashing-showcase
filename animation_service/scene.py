from __future__ import annotations

import json
import os
import textwrap
from pathlib import Path

import numpy as np

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
    RoundedRectangle,
    Arrow,
    CurvedArrow,
    DoubleArrow,
    SMALL_BUFF,
    Line,
    PI,
)


def _load_request_payload() -> dict[str, str]:
    """Load animation data from environment or use defaults."""
    data_path = os.environ.get("ANIMATION_DATA_PATH")
    if not data_path:
        # Default data for testing
        return {
            "message": "This is a test message for the animation.",
            "message_hash_hex": "a1b2c3d4" * 8,
            "signature_base64": "ZmFrZV9zaWduYXR1cmU=",
            "decrypted_hash_hex": "a1b2c3d4" * 8,
            "recomputed_hash_hex": "a1b2c3d4" * 8,
            "verification_status": "Valid",
            "hashes_match": True,
        }

    with Path(data_path).open("r", encoding="utf-8") as handle:
        return json.load(handle)


def _wrap(text: str, width: int = 35, truncate: int = 0) -> str:
    """Wrap text to specified width, with optional truncation."""
    if not text:
        return text

    if truncate > 0 and len(text) > truncate:
        text = text[:truncate] + "..."

    wrapped = "\n".join(
        textwrap.wrap(
            text,
            width=width,
            break_long_words=True,
            replace_whitespace=False,
        )
    )
    return wrapped


class SignatureVisualization(Scene):
    """
    Sequential animation: Sender → Receiver → Check → Verdict.
    Arrows: side-to-side then down, like the notebook sketch.
    """

    def construct(self) -> None:
        data = _load_request_payload()
        message = data["message"]
        message_hash_hex = data["message_hash_hex"]
        signature_b64 = data.get("signature_base64", "")
        decrypted_hash_hex = data.get("decrypted_hash_hex", "")
        recomputed_hash_hex = data.get("recomputed_hash_hex", message_hash_hex)
        verification_status = data.get("verification_status", "Unsigned")
        hashes_match = data.get("hashes_match")

        if hashes_match is None and decrypted_hash_hex and recomputed_hash_hex:
            hashes_match = decrypted_hash_hex == recomputed_hash_hex

        # ==================== TITLE ====================

        title, title_color = self._get_title_and_color(verification_status)
        title.scale(0.8)
        title.to_edge(UP, buff=0.2)
        self.play(FadeIn(title, shift=DOWN * 0.3), run_time=0.8)
        self.wait(1.5)

        # ==================== PHASE 1: SENDER ====================

        self.play(FadeOut(title, run_time=0.5))
        self.wait(0.3)

        sender_label = Text("SENDER", font_size=42, weight="BOLD", color="#FFD700")
        sender_label.move_to([0, 3.4, 0])
        self.play(Write(sender_label), run_time=0.6)
        self.wait(0.8)

        # Sender: Message
        msg_box = self._create_step(
            "Message",
            _wrap(message),
            "#424242",
            scale=0.8
        )
        msg_box.move_to([0, 2.2, 0])
        self.play(FadeIn(msg_box, shift=DOWN * 0.3), run_time=0.6)
        self.wait(1.2)

        # Sender: Hash
        hash_box = self._create_step(
            "Hash (Argon2))",
            _wrap(message_hash_hex),
            "#283593",
            scale=0.8
        )
        hash_box.move_to([0, 0.2, 0])

        # Arrow: right side of Message → right side of Hash (curving to the right)
        arrow1 = CurvedArrow(
            msg_box.get_right() + RIGHT * 0.25,
            hash_box.get_right() + RIGHT * 0.25,
            angle=-PI / 2,
            color="#FFD700",
            stroke_width=6,
            tip_length=0.25,
        )

        self.play(Create(arrow1), run_time=0.5)
        self.play(FadeIn(hash_box, shift=DOWN * 0.3), run_time=0.6)
        self.wait(1.2)

        # Sender: Signature
        sig_box = None
        arrow2 = None
        if verification_status != "Unsigned":
            sig_box = self._create_step(
                "Sign (Private Key)",
                _wrap(signature_b64, truncate=45),
                "#6A1B9A",
                scale=0.8
            )
            sig_box.move_to([0, -1.8, 0])

            # Arrow: left side of Hash → left side of Sign (curving to the left)
            arrow2 = CurvedArrow(
                hash_box.get_left() + LEFT * 0.25,
                sig_box.get_left() + LEFT * 0.25,
                angle=PI / 2,
                color="#FFD700",
                stroke_width=6,
                tip_length=0.25,
            )

            self.play(Create(arrow2), run_time=0.5)
            self.play(FadeIn(sig_box, shift=DOWN * 0.3), run_time=0.6)
            self.wait(1.2)

        sender_objects = [sender_label, msg_box, hash_box, arrow1]
        if sig_box is not None:
            sender_objects.extend([sig_box, arrow2])
        self.play(*[FadeOut(obj, run_time=0.6) for obj in sender_objects])
        self.wait(0.8)

        # ==================== PHASE 2: RECEIVER ====================

        receiver_label = Text(
            "RECEIVER", font_size=42, weight="BOLD", color="#4CAF50"
        )
        receiver_label.move_to([0, 3.4, 0])
        self.play(Write(receiver_label), run_time=0.6)
        self.wait(0.8)

        receiver_objects = [receiver_label]

        if verification_status == "Unsigned":
            msg_box_rx = self._create_step(
                "Received Message", _wrap(message), "#424242", scale=0.8
            )
            msg_box_rx.move_to([0, 1.5, 0])
            self.play(FadeIn(msg_box_rx, shift=DOWN * 0.3), run_time=0.6)

            unsigned_text = Text(
                "⚠ Unsigned\nCannot Verify",
                color="#FF6F00",
                font_size=38,
                weight="BOLD",
                line_spacing=0.8,
            )
            unsigned_text.next_to(msg_box_rx, DOWN, buff=0.8)
            self.play(Write(unsigned_text), run_time=1.0)
            self.wait(2.0)

            receiver_objects.extend([msg_box_rx, unsigned_text])
        else:
            h_space = 3.8

            # --- Left Branch (Recomputation) ---
            msg_box_rx = self._create_step(
                "Received Message", _wrap(message, 25), "#424242", scale=0.8
            )
            msg_box_rx.move_to([-h_space, 1.8, 0])

            hash_box_recomp = self._create_step(
                "Recompute Hash",
                _wrap(recomputed_hash_hex, 25),
                "#1565C0",
                scale=0.8,
            )
            hash_box_recomp.move_to([-h_space, -0.8, 0])

            # Arrow: right side (top-left) → right side (bottom-left)
            arrow_rx1 = CurvedArrow(
                msg_box_rx.get_right() + RIGHT * 0.25,
                hash_box_recomp.get_right() + RIGHT * 0.25,
                angle=-PI / 2,
                color="#4CAF50",
                stroke_width=6,
                tip_length=0.25,
            )

            # --- Right Branch (Decryption) ---
            sig_box_rx = self._create_step(
                "Received Signature", _wrap(signature_b64, 25, truncate=30), "#6A1B9A", scale=0.8
            )
            sig_box_rx.move_to([h_space, 1.8, 0])

            hash_box_decrypt = self._create_step(
                "Decrypt Sig (Public Key)",
                _wrap(decrypted_hash_hex, 25),
                "#00838F",
                scale=0.8,
            )
            hash_box_decrypt.move_to([h_space, -0.8, 0])

            # Arrow: left side (top-right) → left side (bottom-right)
            arrow_rx2 = CurvedArrow(
                sig_box_rx.get_left() + LEFT * 0.25,
                hash_box_decrypt.get_left() + LEFT * 0.25,
                angle=PI / 2,
                color="#4CAF50",
                stroke_width=6,
                tip_length=0.25,
            )

            self.play(
                FadeIn(msg_box_rx, shift=DOWN * 0.3),
                FadeIn(sig_box_rx, shift=DOWN * 0.3),
                run_time=0.6,
            )
            self.wait(1.0)
            self.play(Create(arrow_rx1), Create(arrow_rx2), run_time=0.5)
            self.play(
                FadeIn(hash_box_recomp, shift=DOWN * 0.3),
                FadeIn(hash_box_decrypt, shift=DOWN * 0.3),
                run_time=0.6,
            )
            self.wait(2.5)

            receiver_objects.extend(
                [
                    msg_box_rx,
                    hash_box_recomp,
                    arrow_rx1,
                    sig_box_rx,
                    hash_box_decrypt,
                    arrow_rx2,
                ]
            )

        self.play(*[FadeOut(obj, run_time=0.6) for obj in receiver_objects])
        self.wait(0.8)

        # ==================== PHASE 3: VERIFICATION CHECK ====================

        if verification_status != "Unsigned":
            check_label = Text(
                "VERIFICATION", font_size=42, weight="BOLD", color="#9C27B0"
            )
            check_label.move_to([0, 3.4, 0])
            self.play(Write(check_label), run_time=0.6)
            self.wait(0.8)

            hash_recomp_display = self._create_step(
                "Recomputed", _wrap(recomputed_hash_hex, 25), "#1565C0", scale=0.9
            )
            hash_recomp_display.move_to([-h_space, 1.0, 0])

            hash_decrypt_display = self._create_step(
                "Decrypted", _wrap(decrypted_hash_hex, 25), "#00838F", scale=0.9
            )
            hash_decrypt_display.move_to([h_space, 1.0, 0])

            self.play(
                FadeIn(hash_recomp_display, shift=LEFT * 0.3),
                FadeIn(hash_decrypt_display, shift=RIGHT * 0.3),
                run_time=0.8,
            )
            self.wait(1.0)

            double_arrow = DoubleArrow(
                hash_recomp_display.get_right(),
                hash_decrypt_display.get_left(),
                buff=0.2,
                color="#FFD700",
                stroke_width=10,
                tip_length=0.35,
            )
            self.play(FadeIn(double_arrow), run_time=0.3)
            self.wait(1.5)

            result_str = "✓ MATCH" if hashes_match else "✗ MISMATCH"
            result_color = "#2E7D32" if hashes_match else "#C62828"
            result_text = Text(
                result_str, font_size=48, color=result_color, weight="BOLD"
            )
            result_text.move_to([0, -1.5, 0])
            self.play(Write(result_text), run_time=0.8)
            self.wait(3.0)

            self.play(
                FadeOut(check_label, run_time=0.6),
                FadeOut(hash_recomp_display, run_time=0.6),
                FadeOut(hash_decrypt_display, run_time=0.6),
                FadeOut(double_arrow, run_time=0.6),
                FadeOut(result_text, run_time=0.6),
            )
            self.wait(0.8)

        # ==================== PHASE 4: FINAL VERDICT ====================

        verdict_str = f"Signature is {verification_status.upper()}"
        verdict_color = (
            "#2E7D32"
            if verification_status == "Valid"
            else "#C62828"
            if verification_status == "Invalid"
            else "#FF6F00"
        )
        verdict_text = Text(
            verdict_str, font_size=54, color=verdict_color, weight="BOLD"
        )
        verdict_text.move_to([0, 0, 0])
        self.play(FadeIn(verdict_text, shift=DOWN * 0.5), run_time=1.0)
        self.wait(3.0)
        self.play(FadeOut(verdict_text, run_time=0.8))
        self.wait(0.5)

    def _get_title_and_color(self, status: str) -> tuple[Text, str]:
        text = "Digital Signature Verification"
        color = (
            "#2E7D32"
            if status == "Valid"
            else "#C62828"
            if status == "Invalid"
            else "#FF6F00"
        )
        title = Text(text, font_size=48, color=color, weight="BOLD")
        return title, color

    def _create_step(self, title: str, body: str, color: str, scale: float = 1.0) -> VGroup:
        header = Text(title, font_size=28, color=color, weight="BOLD")
        description = Text(
            body if body else "(empty)",
            font_size=24,
            line_spacing=0.8,
        )
        content = VGroup(header, description).arrange(
            DOWN, buff=0.3, aligned_edge=LEFT
        )
        padding = 0.4
        backdrop = RoundedRectangle(
            width=content.width + padding * 2,
            height=content.height + padding * 2,
            stroke_color=color,
            stroke_width=3,
            fill_color=color,
            fill_opacity=0.08,
            corner_radius=0.1,
        )
        group = VGroup(backdrop, content)
        content.move_to(backdrop.get_center())
        if scale != 1.0:
            group.scale(scale)
        return group
