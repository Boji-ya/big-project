import cv2
import math
import mediapipe as mp


# 1. 設定與初始化 (Tasks API)
options = mp.tasks.vision.HandLandmarkerOptions(
    base_options=mp.tasks.BaseOptions(model_asset_path='C:/big project/model/hand_landmarker.task'),
    num_hands=2,
    running_mode=mp.tasks.vision.RunningMode.IMAGE)

# 21個節點連成線
CONNECTIONS = [(0,1),(1,2),(2,3),(3,4),(0,5),(5,6),(6,7),(7,8),(5,9),(9,10),(10,11),(11,12),(9,13),(13,14),(14,15),(15,16),(13,17),(17,18),(18,19),(19,20),(0,17)]

wrist_history=[]

# 用來暫存平滑後的節點
class SmoothedLandmark:
    def __init__(self, x, y, z):
        self.x = x
        self.y = y
        self.z = z

# 紀錄上一幀的 21 個平滑節點
prev_smoothed_landmarks = None

#=======利用平滑化(EMA) 讓數據更準確==========

def apply_ema_smoothing(current_landmarks, alpha=0.5):
    global prev_smoothed_landmarks
    
    # 如果是第一幀，就直接回傳當前座標並存起來
    if prev_smoothed_landmarks is None:
        prev_smoothed_landmarks = current_landmarks
        return current_landmarks
        
    smoothed_landmarks = []
    
    # 把 21 個節點拿來做平滑計算
    for i in range(len(current_landmarks)):
        curr = current_landmarks[i]
        prev = prev_smoothed_landmarks[i]
        
        # EMA 公式
        #過去的值:現在的值=alpha:(1-alpha)
        smooth_x = alpha * curr.x + (1 - alpha) * prev.x
        smooth_y = alpha * curr.y + (1 - alpha) * prev.y
        smooth_z = alpha * curr.z + (1 - alpha) * prev.z
        
        smoothed_landmarks.append(SmoothedLandmark(smooth_x, smooth_y, smooth_z))
        
    # 將算完的平滑結果存起來
    prev_smoothed_landmarks = smoothed_landmarks
    
    return smoothed_landmarks    



# 2. 手勢判斷邏輯 
def get_distance(p1, p2):
    return math.sqrt((p1.x - p2.x)**2 + (p1.y - p2.y)**2 + (p1.z - p2.z)**2) #求距離公式

def detect_gesture(landmarks, history):
    # 1. 計算基本距離
    palm_size = get_distance(landmarks[0], landmarks[5])
    tip_dist = get_distance(landmarks[4], landmarks[8])

    # 2. 判斷四指是否伸直
    is_index_ext = get_distance(landmarks[8], landmarks[0]) > get_distance(landmarks[5], landmarks[0])  #食指伸直
    is_mid_ext = get_distance(landmarks[12], landmarks[0]) > get_distance(landmarks[9], landmarks[0]) #中指伸直
    is_ring_ext = get_distance(landmarks[16], landmarks[0]) > get_distance(landmarks[13], landmarks[0]) #無名指伸直
    is_pinky_ext = get_distance(landmarks[20], landmarks[0]) > get_distance(landmarks[17], landmarks[0]) #小指伸直

    is_open_palm = is_index_ext and is_mid_ext and is_ring_ext and is_pinky_ext #有沒有張開手掌

    if_good= (landmarks[4].y<landmarks[3].y) #拇指指尖比根部高


    # ==========================================
    # 靜態手勢
    # ==========================================

    # --- 1👌 OK 手勢 -
    if tip_dist < palm_size * 0.3 and is_mid_ext and is_ring_ext and is_pinky_ext: #拇指 食指 碰在一起 且另3指伸直
        history.clear() 
        return "OK (UV Light)"

    # --- 2✌️  YA ---
    elif is_index_ext and is_mid_ext and not is_ring_ext and not is_pinky_ext: #食指 中指伸直 其他彎曲
        history.clear()
        return "V-Sign (Flashbang)"

    # --- 3：比讚 ---
    elif (not is_open_palm) and if_good: #全都彎曲
        history.clear()
        return "Grab"


    # ==========================================
    # 動態手勢
    
    # 手掌張開
    elif is_open_palm:

        history.append(landmarks[0].x)

        #如果紀錄太多的話 刪掉最前面的
        if len(history) > 10:
            history.pop(0)

        if len(history) == 10:
            start_x = history[0]      # 10 幀前的 X
            current_x = history[-1]   # 現在最新的 X
            delta_x = current_x - start_x

            # 判斷向右或向左
            if delta_x > 0.15:
                history.clear() 
                return "Swipe Right --->"
            elif delta_x < -0.15:
                history.clear()
                return "<--- Swipe Left"
                
            return "Open Palm (Tracking...)" # 移動量還不夠大時的顯示
     #跑步   
    elif not is_open_palm:
       # 1. 每一幀都把最新的 Y 座標存進去
        history.append(landmarks[0].y)
        
        # 2. 跑步比較快 記過去 5 幀就好
        if len(history) > 5:
            history.pop(0)
            
        if len(history) == 5:
            start_y = history[0]
            current_y = history[-1]
            delta_y = current_y - start_y

            
            if abs(delta_y) > 0.08:
                history.clear()
                return "Running !!!"
                
        return "Run(Ready...)"

    # 如果什麼手勢都不是
    else:
        history.clear() 
        return "None"


# 3. 繪製手的骨架(未來不需要)
def draw_and_get_gesture(image, result):
    global prev_smoothed_landmarks
    if not result.hand_landmarks: 
        prev_smoothed_landmarks = None
        return image
    
    h, w, _ = image.shape
    for hand_lms, handedness in zip(result.hand_landmarks, result.handedness):
       #呼叫平滑化函數
       smoothed_lms = apply_ema_smoothing(hand_lms, alpha=0.5)
        # 轉換座標 
       px_pts = [(int(p.x * w), int(p.y * h)) for p in smoothed_lms]
        
        # 畫線與畫點
       for s, e in CONNECTIONS: cv2.line(image, px_pts[s], px_pts[e], (255, 255, 255), 2)
       for pt in px_pts: cv2.circle(image, pt, 4, (0, 0, 255), -1)

        # 寫出手勢名稱
       gesture = detect_gesture(hand_lms,wrist_history)

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