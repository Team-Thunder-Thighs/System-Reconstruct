using UnityEngine;
using uOSC;
using System.Collections.Generic;
using EVMC4U;

public class MediaPipeOSCReceiver : MonoBehaviour
{
    [Header("OSC Server Settings")]
    [SerializeField] private int port = 3333;
    [SerializeField] private bool autoStart = true;
    
    [Header("Avatar Settings")]
    [SerializeField] private GameObject avatarModel;
    [SerializeField] private bool enableRootPosition = true;
    [SerializeField] private bool enableBoneTracking = true;
    [SerializeField] private bool enableBlendShapes = false;
    
    [Header("MediaPipe Settings")]
    [SerializeField] private float confidenceThreshold = 0.5f;
    [SerializeField] private bool smoothTracking = true;
    [SerializeField] private float smoothingFactor = 0.8f;
    
    [Header("Coordinate System")]
    [SerializeField] private bool flipX = false;
    [SerializeField] private bool flipY = false;
    [SerializeField] private bool flipZ = false;
    [SerializeField] private Vector3 scaleMultiplier = Vector3.one;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    
    [Header("Advanced Settings")]
    [SerializeField] private bool enableRootTracking = true;
    [SerializeField] private bool enableHandTracking = true;
    [SerializeField] private bool enableFaceTracking = false;
    
    private uOscServer oscServer;
    private ExternalReceiver externalReceiver;
    private Dictionary<int, Vector3> bonePositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Quaternion> boneRotations = new Dictionary<int, Quaternion>();
    private Dictionary<int, Vector3> smoothedPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Quaternion> smoothedRotations = new Dictionary<int, Quaternion>();
    private Vector3 rootPosition = Vector3.zero;
    private Quaternion rootRotation = Quaternion.identity;
    
    // MediaPipe pose landmarks mapping to VMC bone names
    private readonly Dictionary<int, string> poseLandmarksToBones = new Dictionary<int, string>
    {
        // Core body
        { 0, "Head" },           // Nose
        // { 1, "Head" },           // Left eye inner
        // { 2, "Head" },           // Left eye
        // { 3, "Head" },           // Left eye outer
        // { 4, "Head" },           // Right eye inner
        // { 5, "Head" },           // Right eye
        // { 6, "Head" },           // Right eye outer
        // { 7, "Head" },           // Left ear
        // { 8, "Head" },           // Right ear
        // { 9, "Head" },           // Mouth left
        // { 10, "Head" },          // Mouth right
        
        // Upper body
        { 11, "LeftShoulder" },  // Left shoulder
        { 12, "RightShoulder" }, // Right shoulder
        { 13, "LeftUpperArm" },  // Left elbow (corrected)
        { 14, "RightUpperArm" }, // Right elbow (corrected)
        { 15, "LeftLowerArm" },  // Left wrist (corrected)
        { 16, "RightLowerArm" }, // Right wrist (corrected)
        
        // Hands
        { 17, "LeftHand" },      // Left pinky
        { 18, "RightHand" },      // Right pinky
        // { 19, "LeftHand" },      // left index 
        // { 20, "RightHand" },     // right index 
        // { 21, "LeftHand" },     // left thumb 
        // { 22, "RightHand" },     // right thumb
        
        // Lower body
        { 23, "LeftUpperLeg" },  // Left hip
        { 24, "RightUpperLeg" }, // Right hip
        { 25, "LeftLowerLeg" },  // Left knee (corrected)
        { 26, "RightLowerLeg" }, // Right knee (corrected)
        // { 27, "LeftFoot" },      // Left ankle
        // { 28, "RightFoot" },     // Right ankle
        { 29, "LeftFoot" },      // Left heel
        { 30, "RightFoot" },     // Right heel
        // { 31, "LeftFoot" },      // Left foot index
        // { 32, "RightFoot" },     // Right foot index
        
        // Calculated midpoints (using special IDs)
        { 100, "UpperChest" },   // Midpoint of 11 and 12 (shoulders)
        { 101, "Spine" },        // Midpoint of 11, 12, 23, 24 (shoulders and hips)
        { 102, "Hips" },         // Midpoint of 23 and 24 (hips)
    };
    
    // Bone hierarchy for rotation calculations
    private readonly Dictionary<string, string> boneHierarchy = new Dictionary<string, string>
    {
        { "LeftShoulder", "Spine" },
        { "RightShoulder", "Spine" },
        { "LeftArm", "LeftShoulder" },
        { "RightArm", "RightShoulder" },
        { "LeftForeArm", "LeftArm" },
        { "RightForeArm", "RightArm" },
        { "LeftHand", "LeftForeArm" },
        { "RightHand", "RightForeArm" },
        { "LeftUpLeg", "Hips" },
        { "RightUpLeg", "Hips" },
        { "LeftLeg", "LeftUpLeg" },
        { "RightLeg", "RightUpLeg" },
        { "LeftFoot", "LeftLeg" },
        { "RightFoot", "RightLeg" },
    };
    
    void Awake()
    {
        // Get or create OSC server
        oscServer = GetComponent<uOscServer>();
        if (oscServer == null)
        {
            oscServer = gameObject.AddComponent<uOscServer>();
        }
        
        oscServer.port = port;
        oscServer.autoStart = autoStart;
        
        // Get ExternalReceiver for VMC protocol
        externalReceiver = GetComponent<ExternalReceiver>();
        if (externalReceiver == null)
        {
            externalReceiver = gameObject.AddComponent<ExternalReceiver>();
        }
        
        if (avatarModel != null)
        {
            externalReceiver.Model = avatarModel;
        }
    }
    
    void OnEnable()
    {
        if (oscServer != null)
        {
            oscServer.onDataReceived.AddListener(OnOSCMessageReceived);
        }
    }
    
    void OnDisable()
    {
        if (oscServer != null)
        {
            oscServer.onDataReceived.RemoveListener(OnOSCMessageReceived);
        }
    }
    
    void OnOSCMessageReceived(Message message)
    {
        Debug.Log("OnOSCMessageReceived: " + message.address);
        if (message.address == null || message.values == null)
            return;
            
        // Handle different MediaPipe OSC message formats
        if (message.address.StartsWith("/mediapipe/pose"))
        {
            ProcessMediaPipePose(message);
        }
        else if (message.address.StartsWith("/mediapipe/landmarks"))
        {
            ProcessMediaPipeLandmarks(message);
        }
        else if (message.address.StartsWith("/mediapipe/body"))
        {
            ProcessMediaPipeBody(message);
        }
        else if (message.address.StartsWith("/mediapipe/hands"))
        {
            ProcessMediaPipeHands(message);
        }
        else if (message.address.StartsWith("/mediapipe/face"))
        {
            ProcessMediaPipeFace(message);
        }
    }
    
    void ProcessMediaPipePose(Message message)
    {
        // Expected format: /mediapipe/pose [landmark_id] [x] [y] [z] [confidence]
        if (message.values.Length >= 5)
        {
            int landmarkId = (int)message.values[0];
            float x = (float)message.values[1];
            float y = (float)message.values[2];
            float z = (float)message.values[3];
            float confidence = (float)message.values[4];
            
            if (confidence >= confidenceThreshold)
            {
                Vector3 position = new Vector3(x, y, z);
                position = TransformCoordinates(position);
                
                if (poseLandmarksToBones.ContainsKey(landmarkId))
                {
                    string boneName = poseLandmarksToBones[landmarkId];
                    UpdateBonePosition(boneName, position, landmarkId);
                }
            }
        }
    }
    
    void ProcessMediaPipeLandmarks(Message message)
    {
        // Expected format: /mediapipe/landmarks [landmark_id] [x] [y] [z] [confidence]
        if (message.values.Length >= 5)
        {
            int landmarkId = (int)message.values[0];
            float x = (float)message.values[1];
            float y = (float)message.values[2];
            float z = (float)message.values[3];
            float confidence = (float)message.values[4];
            
            if (confidence >= confidenceThreshold)
            {
                Vector3 position = new Vector3(x, y, z);
                position = TransformCoordinates(position);
                
                if (poseLandmarksToBones.ContainsKey(landmarkId))
                {
                    string boneName = poseLandmarksToBones[landmarkId];
                    UpdateBonePosition(boneName, position, landmarkId);
                }
            }
        }
    }
    
    void ProcessMediaPipeBody(Message message)
    {
        // Expected format: /mediapipe/body [landmark_id] [x] [y] [z] [confidence]
        if (message.values.Length >= 5)
        {
            int landmarkId = (int)message.values[0];
            float x = (float)message.values[1];
            float y = (float)message.values[2];
            float z = (float)message.values[3];
            float confidence = (float)message.values[4];
            
            if (confidence >= confidenceThreshold)
            {
                Vector3 position = new Vector3(x, y, z);
                position = TransformCoordinates(position);
                
                if (poseLandmarksToBones.ContainsKey(landmarkId))
                {
                    string boneName = poseLandmarksToBones[landmarkId];
                    UpdateBonePosition(boneName, position, landmarkId);
                }
            }
        }
    }
    
    void ProcessMediaPipeHands(Message message)
    {
        if (!enableHandTracking) return;
        
        // Expected format: /mediapipe/hands [hand_id] [landmark_id] [x] [y] [z] [confidence]
        if (message.values.Length >= 6)
        {
            int handId = (int)message.values[0];
            int landmarkId = (int)message.values[1];
            float x = (float)message.values[2];
            float y = (float)message.values[3];
            float z = (float)message.values[4];
            float confidence = (float)message.values[5];
            
            if (confidence >= confidenceThreshold)
            {
                Vector3 position = new Vector3(x, y, z);
                position = TransformCoordinates(position);
                
                string boneName = handId == 0 ? "LeftHand" : "RightHand";
                UpdateBonePosition(boneName, position, landmarkId + 1000); // Offset for hand landmarks
            }
        }
    }
    
    void ProcessMediaPipeFace(Message message)
    {
        if (!enableFaceTracking) return;
        
        // Expected format: /mediapipe/face [landmark_id] [x] [y] [z] [confidence]
        if (message.values.Length >= 5)
        {
            int landmarkId = (int)message.values[0];
            float x = (float)message.values[1];
            float y = (float)message.values[2];
            float z = (float)message.values[3];
            float confidence = (float)message.values[4];
            
            if (confidence >= confidenceThreshold)
            {
                Vector3 position = new Vector3(x, y, z);
                position = TransformCoordinates(position);
                
                // Map face landmarks to head bone
                UpdateBonePosition("Head", position, landmarkId + 2000); // Offset for face landmarks
            }
        }
    }
    
    Vector3 TransformCoordinates(Vector3 position)
    {
        // Apply coordinate system transformations
        if (flipX) position.x = -position.x;
        if (flipY) position.y = -position.y;
        if (flipZ) position.z = -position.z;
        
        // Apply scale and offset
        position = Vector3.Scale(position, scaleMultiplier);
        position += positionOffset;
        
        return position;
    }
    
    void UpdateBonePosition(string boneName, Vector3 position, int landmarkId)
    {
        if (!enableBoneTracking) return;
        
        // Apply smoothing if enabled
        if (smoothTracking)
        {
            if (smoothedPositions.ContainsKey(landmarkId))
            {
                position = Vector3.Lerp(smoothedPositions[landmarkId], position, 1f - smoothingFactor);
            }
            smoothedPositions[landmarkId] = position;
        }
        
        bonePositions[landmarkId] = position;
        
        // Calculate rotation based on bone direction
        Quaternion rotation = CalculateBoneRotation(boneName, position, landmarkId);
        boneRotations[landmarkId] = rotation;
        
        // Update root position if this is a hip landmark
        if (boneName == "LeftUpLeg" || boneName == "RightUpLeg")
        {
            UpdateRootPosition();
        }
        
        // Calculate and update midpoint bones
        CalculateMidpointBones();
        
        // Send to VMC system
        SendBoneDataToVMC(boneName, position, rotation);
    }
    
    void UpdateRootPosition()
    {
        if (!enableRootPosition || !enableRootTracking) return;
        
        // Calculate root position as average of hip positions
        Vector3 leftHip = Vector3.zero;
        Vector3 rightHip = Vector3.zero;
        bool hasLeftHip = false;
        bool hasRightHip = false;
        
        foreach (var kvp in bonePositions)
        {
            if (poseLandmarksToBones.ContainsKey(kvp.Key))
            {
                string boneName = poseLandmarksToBones[kvp.Key];
                if (boneName == "LeftUpLeg")
                {
                    leftHip = kvp.Value;
                    hasLeftHip = true;
                }
                else if (boneName == "RightUpLeg")
                {
                    rightHip = kvp.Value;
                    hasRightHip = true;
                }
            }
        }
        
        if (hasLeftHip && hasRightHip)
        {
            rootPosition = (leftHip + rightHip) * 0.5f;
            
            // Calculate root rotation based on hip direction
            Vector3 hipDirection = (rightHip - leftHip).normalized;
            rootRotation = Quaternion.LookRotation(hipDirection, Vector3.up);
            
            // Send root data to VMC
            SendRootDataToVMC();
        }
    }
    
    void CalculateMidpointBones()
    {
        if (!enableBoneTracking) return;
        
        // Get required landmark positions
        Vector3 leftShoulder = GetBonePositionByName("LeftShoulder");
        Vector3 rightShoulder = GetBonePositionByName("RightShoulder");
        Vector3 leftHip = GetBonePositionByName("LeftUpLeg");
        Vector3 rightHip = GetBonePositionByName("RightUpLeg");
        
        // Calculate UpperChest (midpoint of shoulders)
        if (leftShoulder != Vector3.zero && rightShoulder != Vector3.zero)
        {
            Vector3 upperChestPos = (leftShoulder + rightShoulder) * 0.5f;
            bonePositions[100] = upperChestPos;
            boneRotations[100] = Quaternion.identity; // UpperChest typically doesn't need rotation
            SendBoneDataToVMC("UpperChest", upperChestPos, Quaternion.identity);
        }
        
        // Calculate Spine (midpoint of shoulders and hips)
        if (leftShoulder != Vector3.zero && rightShoulder != Vector3.zero && 
            leftHip != Vector3.zero && rightHip != Vector3.zero)
        {
            Vector3 spinePos = (leftShoulder + rightShoulder + leftHip + rightHip) * 0.25f;
            bonePositions[101] = spinePos;
            boneRotations[101] = Quaternion.identity; // Spine typically doesn't need rotation
            SendBoneDataToVMC("Spine", spinePos, Quaternion.identity);
        }
        
        // Calculate Hips (midpoint of hips)
        if (leftHip != Vector3.zero && rightHip != Vector3.zero)
        {
            Vector3 hipsPos = (leftHip + rightHip) * 0.5f;
            bonePositions[102] = hipsPos;
            boneRotations[102] = Quaternion.identity; // Hips typically doesn't need rotation
            SendBoneDataToVMC("Hips", hipsPos, Quaternion.identity);
        }
    }
    
    Quaternion CalculateBoneRotation(string boneName, Vector3 position, int landmarkId)
    {
        // Get parent bone position for direction calculation
        if (boneHierarchy.ContainsKey(boneName))
        {
            string parentBoneName = boneHierarchy[boneName];
            Vector3 parentPosition = GetParentBonePosition(parentBoneName);
            
            if (parentPosition != Vector3.zero)
            {
                Vector3 direction = (position - parentPosition).normalized;
                return Quaternion.LookRotation(direction, Vector3.up);
            }
        }
        
        // Fallback: calculate rotation based on bone pairs
        return CalculateRotationFromBonePairs(boneName, position, landmarkId);
    }
    
    Vector3 GetParentBonePosition(string parentBoneName)
    {
        foreach (var kvp in bonePositions)
        {
            if (poseLandmarksToBones.ContainsKey(kvp.Key) && poseLandmarksToBones[kvp.Key] == parentBoneName)
            {
                return kvp.Value;
            }
        }
        return Vector3.zero;
    }
    
    Quaternion CalculateRotationFromBonePairs(string boneName, Vector3 position, int landmarkId)
    {
        // Calculate rotation based on bone pairs (e.g., shoulder to elbow for arm)
        switch (boneName)
        {
            case "LeftArm":
                return CalculateArmRotation("LeftShoulder", "LeftArm", "LeftForeArm");
            case "RightArm":
                return CalculateArmRotation("RightShoulder", "RightArm", "RightForeArm");
            case "LeftLeg":
                return CalculateLegRotation("LeftUpLeg", "LeftLeg", "LeftFoot");
            case "RightLeg":
                return CalculateLegRotation("RightUpLeg", "RightLeg", "RightFoot");
            default:
                return Quaternion.identity;
        }
    }
    
    Quaternion CalculateArmRotation(string shoulder, string elbow, string wrist)
    {
        Vector3 shoulderPos = GetBonePositionByName(shoulder);
        Vector3 elbowPos = GetBonePositionByName(elbow);
        Vector3 wristPos = GetBonePositionByName(wrist);
        
        if (shoulderPos != Vector3.zero && elbowPos != Vector3.zero)
        {
            Vector3 upperArmDir = (elbowPos - shoulderPos).normalized;
            Vector3 forearmDir = wristPos != Vector3.zero ? (wristPos - elbowPos).normalized : Vector3.forward;
            
            Vector3 right = Vector3.Cross(upperArmDir, forearmDir).normalized;
            Vector3 forward = Vector3.Cross(right, upperArmDir).normalized;
            
            return Quaternion.LookRotation(forward, upperArmDir);
        }
        
        return Quaternion.identity;
    }
    
    Quaternion CalculateLegRotation(string hip, string knee, string ankle)
    {
        Vector3 hipPos = GetBonePositionByName(hip);
        Vector3 kneePos = GetBonePositionByName(knee);
        Vector3 anklePos = GetBonePositionByName(ankle);
        
        if (hipPos != Vector3.zero && kneePos != Vector3.zero)
        {
            Vector3 thighDir = (kneePos - hipPos).normalized;
            Vector3 shinDir = anklePos != Vector3.zero ? (anklePos - kneePos).normalized : Vector3.down;
            
            Vector3 right = Vector3.Cross(thighDir, shinDir).normalized;
            Vector3 forward = Vector3.Cross(right, thighDir).normalized;
            
            return Quaternion.LookRotation(forward, thighDir);
        }
        
        return Quaternion.identity;
    }
    
    Vector3 GetBonePositionByName(string boneName)
    {
        foreach (var kvp in bonePositions)
        {
            if (poseLandmarksToBones.ContainsKey(kvp.Key) && poseLandmarksToBones[kvp.Key] == boneName)
            {
                return kvp.Value;
            }
        }
        return Vector3.zero;
    }
    
    void SendBoneDataToVMC(string boneName, Vector3 position, Quaternion rotation)
    {
        // Create VMC protocol message
        var vmcMessage = new Message();
        vmcMessage.address = "/VMC/Ext/Bone/Pos";
        vmcMessage.values = new object[]
        {
            boneName,
            position.x,
            position.y,
            position.z,
            rotation.x,
            rotation.y,
            rotation.z,
            rotation.w
        };
        
        // Send to ExternalReceiver
        if (externalReceiver != null)
        {
            externalReceiver.MessageDaisyChain(ref vmcMessage, 0);
        }
    }
    
    void SendRootDataToVMC()
    {
        if (!enableRootPosition) return;
        
        // Create VMC protocol message for root position
        var vmcMessage = new Message();
        vmcMessage.address = "/VMC/Ext/Root/Pos";
        vmcMessage.values = new object[]
        {
            "Root",
            rootPosition.x,
            rootPosition.y,
            rootPosition.z,
            rootRotation.x,
            rootRotation.y,
            rootRotation.z,
            rootRotation.w
        };
        
        // Send to ExternalReceiver
        if (externalReceiver != null)
        {
            externalReceiver.MessageDaisyChain(ref vmcMessage, 0);
        }
    }
    
    // Public methods for external control
    public void SetConfidenceThreshold(float threshold)
    {
        confidenceThreshold = Mathf.Clamp01(threshold);
    }
    
    public void SetSmoothingFactor(float factor)
    {
        smoothingFactor = Mathf.Clamp01(factor);
    }
    
    public void SetCoordinateTransform(bool flipX, bool flipY, bool flipZ)
    {
        this.flipX = flipX;
        this.flipY = flipY;
        this.flipZ = flipZ;
    }
    
    public void SetScaleMultiplier(Vector3 scale)
    {
        scaleMultiplier = scale;
    }
    
    public void SetPositionOffset(Vector3 offset)
    {
        positionOffset = offset;
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Draw all bone positions with different colors based on type
        foreach (var kvp in bonePositions)
        {
            // Determine color based on landmark ID
            if (kvp.Key >= 100) // Calculated midpoint bones
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(kvp.Value, 0.15f);
                
                // Add labels for midpoint bones
                #if UNITY_EDITOR
                if (poseLandmarksToBones.ContainsKey(kvp.Key))
                {
                    string boneName = poseLandmarksToBones[kvp.Key];
                    UnityEditor.Handles.Label(kvp.Value + Vector3.up * 0.2f, boneName);
                }
                #endif
            }
            else if (kvp.Key == 0) // Head
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(kvp.Value, 0.12f);
            }
            else if (kvp.Key >= 11 && kvp.Key <= 16) // Upper body (shoulders, arms)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(kvp.Value, 0.1f);
            }
            else if (kvp.Key >= 17 && kvp.Key <= 18) // Hands
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(kvp.Value, 0.08f);
            }
            else if (kvp.Key >= 23 && kvp.Key <= 30) // Lower body (hips, legs, feet)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(kvp.Value, 0.1f);
            }
            else // Other landmarks
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(kvp.Value, 0.08f);
            }
        }
        
        // Draw root position
        if (enableRootPosition)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rootPosition, 0.2f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(rootPosition + Vector3.up * 0.3f, "Root");
            #endif
        }
        
        // Draw connections between related bones for better visualization
        DrawBoneConnections();
    }
    
    void DrawBoneConnections()
    {
        if (!Application.isPlaying) return;
        
        Gizmos.color = Color.white;
        
        // Draw shoulder line
        Vector3 leftShoulder = GetBonePositionByName("LeftShoulder");
        Vector3 rightShoulder = GetBonePositionByName("RightShoulder");
        if (leftShoulder != Vector3.zero && rightShoulder != Vector3.zero)
        {
            Gizmos.DrawLine(leftShoulder, rightShoulder);
        }
        
        // Draw hip line
        Vector3 leftHip = GetBonePositionByName("LeftUpLeg");
        Vector3 rightHip = GetBonePositionByName("RightUpLeg");
        if (leftHip != Vector3.zero && rightHip != Vector3.zero)
        {
            Gizmos.DrawLine(leftHip, rightHip);
        }
        
        // Draw spine line (shoulder midpoint to hip midpoint)
        Vector3 upperChest = GetBonePositionByName("UpperChest");
        Vector3 hips = GetBonePositionByName("Hips");
        if (upperChest != Vector3.zero && hips != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(upperChest, hips);
        }
    }
} 