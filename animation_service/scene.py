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
def from __future__ import annotations

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
    """Load the animation data from the path specified in the environment variable."""
    data_path = os.environ.get("ANIMATION_DATA_PATH")
    if not data_path:
        # Fallback for local testing if the env var is not set
        return {
            "message": "This is a test message for the animation.",
            "message_hash_hex": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
            "signature_base64": "e5f6g7h8i9j0e5f6g7h8i9j0e5f6g7h8i9j0e5f6g7h8i9j0e5f6g7h8i9j0e5f6",
            "decrypted_hash_hex": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
            "recomputed_hash_hex": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
            "verification_status": "Valid",
            "hashes_match": True,
        }
    with Path(data_path).open("r", encoding="utf-8") as handle:
        return json.load(handle)

def _wrap(text: str, width: int = 60) -> str:
    """Wrap text to a specified width."""
    if not text:
        return text
    return "\n".join(textwrap.wrap(text, width=width))

class SignatureVisualization(Scene):
    """Visualize hashing and RSA signing using the provided payload."""

    def construct(self) -> None:  # noqa: D401 - manim entry point
        """Define the animation sequence."""
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

        # 1. --- Title ---
        title, title_color = self._get_title_and_color(verification_status)
        self.play(FadeIn(title, shift=DOWN * 0.5))
        self.wait(1)

        # 2. --- Sender Side ---
        sender_label = Text("Sender", font_size=36).to_edge(UP + LEFT, buff=1)
        self.play(Write(sender_label))

        # Create and show message
        msg_box = self._create_step("1. Plain Message", message, "#424242")
        msg_box.next_to(sender_label, DOWN, buff=0.5, aligned_edge=LEFT)
        self.play(FadeIn(msg_box, shift=RIGHT * 0.5))
        self.wait(1)

        # Animate hashing
        hash_box = self._create_step("2. Hash (SHA-256)", message_hash_hex, "#283593")
        hash_box.next_to(msg_box, DOWN, buff=1.5)
        arrow1 = Arrow(msg_box.get_bottom(), hash_box.get_top(), buff=0.2)
        self.play(Create(arrow1))
        self.play(FadeIn(hash_box, shift=RIGHT * 0.5))
        self.wait(1)

        if verification_status != "Unsigned":
            # Animate signing
            signature_box = self._create_step("3. Sign with Private Key", signature_b64, "#6A1B9A")
            signature_box.next_to(hash_box, DOWN, buff=1.5)
            arrow2 = Arrow(hash_box.get_bottom(), signature_box.get_top(), buff=0.2)
            self.play(Create(arrow2))
            self.play(FadeIn(signature_box, shift=RIGHT * 0.5))
            self.wait(2)

            # Group what is sent
            sent_group = VGroup(msg_box.copy(), signature_box.copy())
        else:
            # If unsigned, only the message is "sent"
            sent_group = VGroup(msg_box.copy())

        # Animate sending to receiver
        receiver_label = Text("Receiver", font_size=36).to_edge(UP + RIGHT, buff=1)
        self.play(Write(receiver_label))
        self.play(sent_group.animate.next_to(receiver_label, DOWN, buff=0.5, aligned_edge=RIGHT))
        self.wait(1)

        # 3. --- Receiver Side ---
        if verification_status == "Unsigned":
            # Handle unsigned message
            unsigned_text = Text("Message is unsigned. Cannot verify.", color="#FF6F00", font_size=32)
            unsigned_text.next_to(sent_group, DOWN, buff=1)
            self.play(Write(unsigned_text))
            self.wait(3)
        else:
            # Separate message and signature
            received_msg = sent_group.submobjects[0]
            received_sig = sent_group.submobjects[1]
            self.play(received_msg.animate.shift(LEFT * 2), received_sig.animate.shift(RIGHT * 2))
            self.wait(1)

            # Path A: Re-hash the message
            recomputed_hash_box = self._create_step("4a. Recompute Hash", recomputed_hash_hex, "#1565C0")
            recomputed_hash_box.next_to(received_msg, DOWN, buff=1)
            arrow3a = Arrow(received_msg.get_bottom(), recomputed_hash_box.get_top(), buff=0.2)
            self.play(Create(arrow3a))
            self.play(FadeIn(recomputed_hash_box))
            self.wait(1)

            # Path B: Decrypt the signature
            decrypted_hash_box = self._create_step("4b. Decrypt Signature (Public Key)", decrypted_hash_hex, "#00838F")
            decrypted_hash_box.next_to(received_sig, DOWN, buff=1)
            arrow3b = Arrow(received_sig.get_bottom(), decrypted_hash_box.get_top(), buff=0.2)
            self.play(Create(arrow3b))
            self.play(FadeIn(decrypted_hash_box))
            self.wait(2)

            # 5. --- Comparison ---
            compare_label = Text("5. Compare Hashes", font_size=32).move_to(self.camera.frame_center + DOWN * 1.5)
            self.play(Write(compare_label))
            self.play(
                recomputed_hash_box.animate.next_to(compare_label, LEFT, buff=1),
                decrypted_hash_box.animate.next_to(compare_label, RIGHT, buff=1),
            )
            self.wait(1)

            # Show result
            result_text_str = "✓ Hashes Match" if hashes_match else "✗ Hashes Do Not Match"
            result_color = "#2E7D32" if hashes_match else "#C62828"
            result_text = Text(result_text_str, font_size=36, color=result_color)
            result_text.next_to(compare_label, DOWN, buff=1)
            self.play(Write(result_text))
            self.wait(2)

            # Final verification status
            final_status_str = f"Signature is {verification_status.upper()}"
            final_status = Text(final_status_str, font_size=40, color=title_color)
            final_status.to_edge(DOWN, buff=1)
            self.play(FadeIn(final_status, shift=UP))
            self.wait(3)

        # 6. --- Fade Out ---
        self.play(*[FadeOut(mob) for mob in self.mobjects])
        self.wait(1)

    def _get_title_and_color(self, status: str) -> tuple[Text, str]:
        """Return the title Text object and color based on verification status."""
        if status == "Valid":
            text = "Digital Signature Journey - Valid"
            color = "#2E7D32"  # Green
        elif status == "Invalid":
            text = "Digital Signature Journey - Invalid"
            color = "#C62828"  # Red
        else:  # Unsigned
            text = "Digital Signature Journey - Unsigned"
            color = "#FF6F00"  # Orange

        title = Text(text, font_size=44, color=color).to_edge(UP)
        return title, color

    def _create_step(self, title: str, body: str, color: str) -> VGroup:
        """Create a styled box with a title and body."""
        header = Text(title, font_size=24, color=color, weight="BOLD")
        body_text = _wrap(body, width=35)
        description = Text(body_text if body_text else "(empty)", font_size=20, line_spacing=0.8)

        content_group = VGroup(header, description).arrange(DOWN, buff=0.3, aligned_edge=LEFT)

        box_padding = 0.4
        backdrop = Rectangle(
            width=content_group.width + box_padding * 2,
            height=content_group.height + box_padding * 2,
            stroke_color=color,
            stroke_width=2,
            fill_color=color,
            fill_opacity=0.08,
        )

        group = VGroup(backdrop, content_group)
        content_group.move_to(backdrop.get_center())
        return group


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

        # 1. --- Title ---
        title, title_color = self._get_title_and_color(verification_status)
        self.play(FadeIn(title, shift=DOWN * 0.5))
        self.wait(1)

        # 2. --- Sender Side ---
        sender_label = Text("Sender", font_size=36).to_edge(UP + LEFT, buff=1)
        self.play(Write(sender_label))

        # Create and show message
        msg_box = self._create_step("1. Plain Message", _wrap(message), "#424242")
        msg_box.next_to(sender_label, DOWN, buff=0.5, aligned_edge=LEFT)
        self.play(FadeIn(msg_box, shift=RIGHT * 0.5))
        self.wait(1)

        # Animate hashing
        hash_box = self._create_step("2. Hash (SHA-256)", _wrap(message_hash_hex), "#283593")
        hash_box.next_to(msg_box, DOWN, buff=1.5)
        arrow1 = Arrow(msg_box.get_bottom(), hash_box.get_top(), buff=0.2)
        self.play(Create(arrow1))
        self.play(FadeIn(hash_box, shift=RIGHT * 0.5))
        self.wait(1)

        if verification_status != "Unsigned":
            # Animate signing
            signature_box = self._create_step("3. Sign with Private Key", _wrap(signature_b64), "#6A1B9A")
            signature_box.next_to(hash_box, DOWN, buff=1.5)
            arrow2 = Arrow(hash_box.get_bottom(), signature_box.get_top(), buff=0.2)
            self.play(Create(arrow2))
            self.play(FadeIn(signature_box, shift=RIGHT * 0.5))
            self.wait(2)

            # Group what is sent
            sent_group = VGroup(msg_box.copy(), signature_box.copy())
        else:
            # If unsigned, only the message is "sent"
            sent_group = VGroup(msg_box.copy())

        # Animate sending to receiver
        receiver_label = Text("Receiver", font_size=36).to_edge(UP + RIGHT, buff=1)
        self.play(Write(receiver_label))
        self.play(sent_group.animate.next_to(receiver_label, DOWN, buff=0.5, aligned_edge=RIGHT))
        self.wait(1)

        # 3. --- Receiver Side ---
        if verification_status == "Unsigned":
            # Handle unsigned message
            unsigned_text = Text("Message is unsigned. Cannot verify.", color="#FF6F00", font_size=32)
            unsigned_text.next_to(sent_group, DOWN, buff=1)
            self.play(Write(unsigned_text))
            self.wait(3)
        else:
            # Separate message and signature
            received_msg = sent_group.submobjects[0]
            received_sig = sent_group.submobjects[1]
            self.play(received_msg.animate.shift(LEFT * 2), received_sig.animate.shift(RIGHT * 2))
            self.wait(1)

            # Path A: Re-hash the message
            recomputed_hash_box = self._create_step("4a. Recompute Hash", _wrap(recomputed_hash_hex), "#1565C0")
            recomputed_hash_box.next_to(received_msg, DOWN, buff=1)
            arrow3a = Arrow(received_msg.get_bottom(), recomputed_hash_box.get_top(), buff=0.2)
            self.play(Create(arrow3a))
            self.play(FadeIn(recomputed_hash_box))
            self.wait(1)

            # Path B: Decrypt the signature
            decrypted_hash_box = self._create_step("4b. Decrypt Signature (Public Key)", _wrap(decrypted_hash_hex), "#00838F")
            decrypted_hash_box.next_to(received_sig, DOWN, buff=1)
            arrow3b = Arrow(received_sig.get_bottom(), decrypted_hash_box.get_top(), buff=0.2)
            self.play(Create(arrow3b))
            self.play(FadeIn(decrypted_hash_box))
            self.wait(2)

            # 5. --- Comparison ---
            compare_label = Text("5. Compare Hashes", font_size=32).move_to(self.camera.frame_center + DOWN * 1.5)
            self.play(Write(compare_label))
            self.play(
                recomputed_hash_box.animate.next_to(compare_label, LEFT, buff=1),
                decrypted_hash_box.animate.next_to(compare_label, RIGHT, buff=1),
            )
            self.wait(1)

            # Show result
            result_text_str = "✓ Hashes Match" if hashes_match else "✗ Hashes Do Not Match"
            result_color = "#2E7D32" if hashes_match else "#C62828"
            result_text = Text(result_text_str, font_size=36, color=result_color)
            result_text.next_to(compare_label, DOWN, buff=1)
            self.play(Write(result_text))
            self.wait(2)

            # Final verification status
            final_status_str = f"Signature is {verification_status.upper()}"
            final_status = Text(final_status_str, font_size=40, color=title_color)
            final_status.to_edge(DOWN, buff=1)
            self.play(FadeIn(final_status, shift=UP))
            self.wait(3)

        # 6. --- Fade Out ---
        self.play(*[FadeOut(mob) for mob in self.mobjects])
        self.wait(1)

    def _get_title_and_color(self, status: str) -> tuple[Text, str]:
        if status == "Valid":
            text = "Digital Signature Journey - Valid"
            color = "#2E7D32"  # Green
        elif status == "Invalid":
            text = "Digital Signature Journey - Invalid"
            color = "#C62828"  # Red
        else:  # Unsigned
            text = "Digital Signature Journey - Unsigned"
            color = "#FF6F00"  # Orange

        title = Text(text, font_size=44, color=color).to_edge(UP)
        return title, color

    def _create_step(self, title: str, body: str, color: str) -> VGroup:
        header = Text(title, font_size=24, color=color, weight="BOLD")
        body_text = _wrap(body, width=35)
        description = Text(body_text if body_text else "(empty)", font_size=20, line_spacing=0.8)
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
