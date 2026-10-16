import sys
import json
import os
import cv2
import numpy as np

def process_portrait(input_path, output_path):
    if not os.path.exists(input_path):
        return {"success": False, "error": f"Input file not found: {input_path}"}
    
    img = cv2.imread(input_path)
    if img is None:
        return {"success": False, "error": "Failed to decode input image"}
    
    h, w = img.shape[:2]
    
    cascade_path = cv2.data.haarcascades + 'haarcascade_frontalface_default.xml'
    face_cascade = cv2.CascadeClassifier(cascade_path)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    faces = face_cascade.detectMultiScale(gray, scaleFactor=1.1, minNeighbors=3, minSize=(35, 35))
    
    if len(faces) == 0:
        faces = face_cascade.detectMultiScale(gray, scaleFactor=1.08, minNeighbors=2, minSize=(30, 30))
        
    face_found = len(faces) > 0
    if face_found:
        faces = sorted(faces, key=lambda f: f[2] * f[3], reverse=True)
        fx, fy, fw, fh = faces[0]
        
        # Expand head bounding box (hair top, chin & neck bottom, ears left/right)
        top = max(0, int(fy - fh * 0.40))
        bottom = min(h, int(fy + fh + fh * 0.45))
        left = max(0, int(fx - fw * 0.28))
        right = min(w, int(fx + fw + fw * 0.28))
    else:
        # Fallback to centered vertical portrait crop (3:4 ratio)
        cw = int(min(w, h * 0.75))
        ch = int(min(h, w * 1.33))
        left = max(0, (w - cw) // 2)
        top = max(0, int(h * 0.05))
        right = min(w, left + cw)
        bottom = min(h, top + ch)
        fx, fy, fw, fh = left + cw // 4, top + ch // 4, cw // 2, ch // 2
        
    crop = img[top:bottom, left:right]
    ch, cw = crop.shape[:2]
    
    if ch < 10 or cw < 10:
        return {"success": False, "error": "Cropped region is too small"}
        
    # High-speed multiscale GrabCut: downscale to ~200px width for fast computation
    small_w = min(200, cw)
    scale = small_w / float(cw)
    small_h = max(10, int(ch * scale))
    small_crop = cv2.resize(crop, (small_w, small_h))
    
    mask = np.zeros((small_h, small_w), np.uint8)
    mask[:, :] = cv2.GC_BGD
    margin_x = max(1, int(small_w * 0.08))
    margin_y = max(1, int(small_h * 0.06))
    mask[margin_y:small_h - margin_y, margin_x:small_w - margin_x] = cv2.GC_PR_FGD
    
    rel_fx = int(max(0, fx - left) * scale)
    rel_fy = int(max(0, fy - top) * scale)
    rel_fw = int(fw * scale)
    rel_fh = int(fh * scale)
    cv2.ellipse(mask, (rel_fx + rel_fw // 2, rel_fy + rel_fh // 2), 
                (max(5, int(rel_fw * 0.35)), max(5, int(rel_fh * 0.40))), 0, 0, 360, cv2.GC_FGD, -1)
    
    try:
        bgdModel = np.zeros((1, 65), np.float64)
        fgdModel = np.zeros((1, 65), np.float64)
        cv2.grabCut(small_crop, mask, None, bgdModel, fgdModel, 2, cv2.GC_INIT_WITH_MASK)
        small_gc_mask = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype('uint8')
        gc_mask = cv2.resize(small_gc_mask, (cw, ch), interpolation=cv2.INTER_LINEAR)
    except Exception:
        gc_mask = np.full((ch, cw), 255, dtype=np.uint8)
        
    # Smooth ellipse portrait mask to guarantee clean shape and remove boundary noise
    ellipse_mask = np.zeros((ch, cw), dtype=np.uint8)
    mask_cy = int(ch * 0.46)
    cv2.ellipse(ellipse_mask, (cw // 2, mask_cy), 
                (int(cw * 0.47), int(ch * 0.49)), 0, 0, 360, 255, -1)
                
    combined_mask = cv2.bitwise_and(gc_mask, ellipse_mask)
    
    # Soft feathering on the edges for natural blending
    smooth_mask = cv2.GaussianBlur(combined_mask, (19, 19), 0)
    
    # Soft vertical fade on the bottom 25% for smooth neck tucking into shirt collar
    fade_len = int(ch * 0.25)
    if fade_len > 0:
        for i in range(fade_len):
            factor = i / float(fade_len)
            row = ch - fade_len + i
            smooth_mask[row, :] = (smooth_mask[row, :] * factor).astype(np.uint8)
            
    bgra = cv2.cvtColor(crop, cv2.COLOR_BGR2BGRA)
    bgra[:, :, 3] = smooth_mask
    
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    cv2.imwrite(output_path, bgra)
    
    return {
        "success": True,
        "faceDetected": face_found,
        "cropWidth": cw,
        "cropHeight": ch,
        "outputPath": output_path
    }

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(json.dumps({"success": False, "error": "Usage: crop_portrait.py <input_path> <output_path>"}))
        sys.exit(1)
        
    res = process_portrait(sys.argv[1], sys.argv[2])
    print(json.dumps(res))
