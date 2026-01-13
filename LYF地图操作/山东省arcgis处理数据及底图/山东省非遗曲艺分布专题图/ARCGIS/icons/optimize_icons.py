
import os
from PIL import Image

def resize_images(directory, size=(64, 64)):
    print(f"Scanning directory: {directory}")
    for filename in os.listdir(directory):
        if filename.lower().endswith(".png"):
            filepath = os.path.join(directory, filename)
            try:
                with Image.open(filepath) as img:
                    # Convert to RGBA to preserve transparency
                    img = img.convert("RGBA")
                    # Resize using high-quality downsampling
                    img = img.resize(size, Image.Resampling.LANCZOS)
                    # Save back to the same file
                    img.save(filepath, "PNG", optimize=True)
                    print(f"Resized and saved: {filename}")
            except Exception as e:
                print(f"Failed to process {filename}: {e}")

if __name__ == "__main__":
    target_dir = r"c:/Users/24147/xwechat_files/wxid_dm4f57ypxzt722_21a2/msg/file/2026-01/ARCGIS/ARCGIS/icons"
    resize_images(target_dir)
