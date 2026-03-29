import cv2
import math
import mediapipe as mp
import time
# 1. 設定與初始化 (Tasks API)
options = mp.tasks.vision.HandLandmarkerOptions(
    base_options=mp.tasks.BaseOptions(model_asset_path='C:/big project/model/hand_landmarker.task'),
    num_hands=2,
    running_mode=mp.tasks.vision.RunningMode.IMAGE)

# 21個節點連成線
CONNECTIONS = [(0,1),(1,2),(2,3),(3,4),(0,5),(5,6),(6,7),(7,8),(5,9),(9,10),(10,11),(11,12),(9,13),(13,14),(14,15),(15,16),(13,17),(17,18),(18,19),(19,20),(0,17)]

wrist_history=[]


# --- Debug 專用---
freeze_timer = 0
freeze_text = ""

# 專門用來讓字停留在畫面上的小工具
def hold_text_for_debug(raw_gesture):
    global freeze_timer, freeze_text
    
    # 只要看到 "Swipe" 出現，就把它記下來，並設定倒數 15 幀 (約 0.5 秒)
    if "swipe" in raw_gesture:
        freeze_text = raw_gesture
        freeze_timer = 15

    # 如果倒數計時還沒結束，就強制回傳凍結的文字
    if freeze_timer > 0:
        freeze_timer -= 1
        return freeze_text
        
    # 平常沒事就回傳原本的手勢
    return raw_gesture



# 2. 手勢判斷邏輯 
def get_distance(p1, p2):
    return math.sqrt((p1.x - p2.x)**2 + (p1.y - p2.y)**2 + (p1.z - p2.z)**2) #求距離公式

def detect_gesture(landmarks,history):
    # 取得關鍵點 (0:手腕, 4:拇指尖, 5:食指根, 8:食指尖, 9:中指根, 12:中指尖)
    palm_size = get_distance(landmarks[0], landmarks[5])#手掌大小
    tip_dist = get_distance(landmarks[4], landmarks[8]) #拇指到食指指尖的距離

    # 3. 判斷四根手指是否「伸直」
    # 原理：指尖(Tip)到手腕(0)的距離 > 指根(MCP)到手腕(0)的距離

    is_index_ext = get_distance(landmarks[8], landmarks[0]) > get_distance(landmarks[5], landmarks[0]) #食指是否有伸直
    is_mid_ext = get_distance(landmarks[12], landmarks[0]) > get_distance(landmarks[9], landmarks[0]) #中指是否有伸直
    is_ring_ext = get_distance(landmarks[16], landmarks[0]) > get_distance(landmarks[13], landmarks[0])#無名指是否有伸直
    is_pinky_ext = get_distance(landmarks[20], landmarks[0]) > get_distance(landmarks[17], landmarks[0])#小指是否有伸直
    is_open_palm= is_index_ext and  is_mid_ext and is_ring_ext and  is_pinky_ext


    # 判斷姿勢 
   #靜態判斷
    # ok判斷:拇指食指碰在一起 且3指伸直
    if tip_dist < palm_size * 0.3 and is_mid_ext and is_ring_ext and is_pinky_ext:
            return "OK (UV Light)"
    # 條件：食指與中指伸直，無名指與小拇指必須彎曲
    elif is_index_ext and is_mid_ext and not is_ring_ext and not is_pinky_ext:
            return "V-Sign (Flashbang)"
    elif not is_index_ext and  not is_mid_ext and not is_ring_ext and not is_pinky_ext:
            return "0"


    #動態判斷
         
    elif is_open_palm:

        if len(history) == 0:
            history.append(landmarks[0].x)
        else:
            start_x = history[0]
            current_x = landmarks[0].x
            delta_x = current_x - start_x

            if(delta_x>0.1):
                history.clear()
                return"swipe left"
            elif(delta_x<-0.1):
                history.clear()
                return "swipe right"


# 3. 繪製手的骨架(未來不需要)
def draw_and_get_gesture(image, result):
    if not result.hand_landmarks: return image
    
    h, w, _ = image.shape
    for hand_lms, handedness in zip(result.hand_landmarks, result.handedness):
        # 轉換座標
        px_pts = [(int(p.x * w), int(p.y * h)) for p in hand_lms]
        
        # 畫線與畫點
        for s, e in CONNECTIONS: cv2.line(image, px_pts[s], px_pts[e], (255, 255, 255), 2)
        for pt in px_pts: cv2.circle(image, pt, 4, (0, 0, 255), -1)

        # 寫出手勢名稱
        gesture = detect_gesture(hand_lms,wrist_history)

        #=========debug用=======#
        # 1. 取得真實瞬間的手勢判斷
        raw_gesture = detect_gesture(hand_lms, wrist_history)
        
        # ==========================================
        # 2. 測試用的視覺凍結 (接到 Unity 時，把這行註解掉，並把下面的變數改回 raw_gesture 即可)
        display_text = hold_text_for_debug(raw_gesture)
        # ==========================================

        # 3. 畫在畫面上
        cv2.putText(image, display_text, (px_pts[0][0] - 20, px_pts[0][1] - 20), 
                    cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2)
        #=========#
        cv2.putText(image, f"{handedness[0].category_name}: {gesture}", 
                    (px_pts[0][0] - 20, px_pts[0][1] - 20), 
                    cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 255), 2)
    return image


# 4. 主程式迴圈 
cap = cv2.VideoCapture(0)

with mp.tasks.vision.HandLandmarker.create_from_options(options) as landmarker:
    while cap.isOpened():
        success, frame = cap.read()
        if not success: break

        # 轉換格式並讓模型偵測
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        result = landmarker.detect(mp_image)
        
        # 畫圖與顯示
        frame = draw_and_get_gesture(frame, result)
        cv2.imshow('MR Game Controller', frame)
        
        if cv2.waitKey(5) == ord('q'): break

cap.release()
cv2.destroyAllWindows()