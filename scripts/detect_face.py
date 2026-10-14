import sys
import json
import os
import cv2

def detect(image_path):
    if not os.path.exists(image_path):
        return {"success": False, "error": f"Image file not found: {image_path}"}
    
    img = cv2.imread(image_path)
    if img is None:
        return {"success": False, "error": "Failed to decode image"}
    
    h, w = img.shape[:2]
    cascade_path = cv2.data.haarcascades + 'haarcascade_frontalface_default.xml'
    face_cascade = cv2.CascadeClassifier(cascade_path)
    
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    faces = face_cascade.detectMultiScale(gray, scaleFactor=1.1, minNeighbors=3, minSize=(30, 30))
    
    results = []
    for (x, y, fw, fh) in faces:
        # Pad face bounding box slightly so it covers chin, hair, and collar connection
        pad_top = int(fh * 0.2)
        pad_bottom = int(fh * 0.2)
        pad_side = int(fw * 0.12)
        
        box_x = max(0, x - pad_side)
        box_y = max(0, y - pad_top)
        box_w = min(w - box_x, fw + 2 * pad_side)
        box_h = min(h - box_y, fh + pad_top + pad_bottom)
        
        results.append({
            "x": round((box_x / w) * 100, 2),
            "y": round((box_y / h) * 100, 2),
            "width": round((box_w / w) * 100, 2),
            "height": round((box_h / h) * 100, 2),
            "centerX": round(((box_x + box_w / 2) / w) * 100, 2),
            "centerY": round(((box_y + box_h / 2) / h) * 100, 2)
        })
    
    results.sort(key=lambda f: f["width"] * f["height"], reverse=True)
    
    return {
        "success": True,
        "imageWidth": w,
        "imageHeight": h,
        "faceCount": len(results),
        "faces": results
    }

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"success": False, "error": "No image path provided"}))
        sys.exit(1)
    
    res = detect(sys.argv[1])
    print(json.dumps(res))
