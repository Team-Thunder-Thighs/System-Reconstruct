# MediaPipe TouchDesigner Plugin + Unity VRM Avatar Setup Guide

## Overview
This guide shows how to use the [MediaPipe TouchDesigner Plugin](https://github.com/torinmb/mediapipe-touchdesigner) to control a VRM avatar in Unity via OSC communication.

## Prerequisites
- TouchDesigner (2022.1 or later)
- Unity 2022.3 LTS or later
- MediaPipe TouchDesigner Plugin (from GitHub)
- Webcam or video input

## MediaPipe TouchDesigner Plugin Setup

### 1. Install the Plugin
1. Download the plugin from: https://github.com/torinmb/mediapipe-touchdesigner
2. Follow the installation instructions in the plugin's README
3. The plugin provides GPU-accelerated MediaPipe integration

### 2. Plugin Features
The MediaPipe TouchDesigner plugin provides:
- **Pose Detection** (33 landmarks)
- **Hand Tracking** (21 landmarks per hand)
- **Face Mesh** (468 landmarks)
- **GPU acceleration** for better performance
- **Built-in OSC output** capabilities
- **Real-time processing** with low latency

## Unity Setup

### 1. Import Required Packages
Ensure you have these packages in your Unity project:
- **uOSC** (already included)
- **EVMC4U** (already included)
- **VRM** (already included)
- **UniGLTF** (already included)

### 2. Add MediaPipeOSCReceiver Script
1. Create a new GameObject in your scene
2. Add the `MediaPipeOSCReceiver.cs` script to it
3. Configure the settings:
   - **Port**: 3333 (default)
   - **Avatar Model**: Drag your VRM avatar prefab here
   - **Confidence Threshold**: 0.5 (adjust as needed)
   - **Smoothing Factor**: 0.8 (for smooth movement)

## TouchDesigner Network Setup with Plugin

### 1. Basic Network Structure
```
[Camera In TOP] -> [MediaPipe Plugin] -> [OSC Out DAT] -> Unity
```

### 2. Plugin Configuration
1. **Add MediaPipe Plugin TOP** to your network
2. **Connect Camera In TOP** to the plugin input
3. **Configure Plugin Settings**:
   - Enable **Pose Detection**
   - Enable **Hand Tracking** (optional)
   - Enable **Face Mesh** (optional)
   - Set **Detection Confidence** to 0.5-0.7
   - Set **Tracking Confidence** to 0.5-0.7

### 3. OSC Output Setup
The plugin may have built-in OSC output. If not, create an OSC Out DAT:

**OSC Out DAT Settings:**
- **Host**: 127.0.0.1
- **Port**: 3333
- **Message Format**: Depends on plugin output format

### 4. Data Extraction from Plugin
If the plugin doesn't have built-in OSC output, use a Python Script DAT to extract data:

```python
# Python Script DAT to extract MediaPipe data from plugin
def onFrameStart(frame):
    # Get MediaPipe plugin TOP
    mediapipe_top = op('mediapipe_plugin')  # Replace with your plugin TOP name
    
    if mediapipe_top:
        # Extract pose landmarks
        pose_data = mediapipe_top.fetch('pose_landmarks')
        if pose_data:
            send_pose_data(pose_data)
        
        # Extract hand landmarks
        hand_data = mediapipe_top.fetch('hand_landmarks')
        if hand_data:
            send_hand_data(hand_data)
        
        # Extract face landmarks
        face_data = mediapipe_top.fetch('face_landmarks')
        if face_data:
            send_face_data(face_data)

def send_pose_data(pose_data):
    # Send pose landmarks via OSC
    osc_out = op('osc_out')  # Replace with your OSC Out DAT name
    
    for i, landmark in enumerate(pose_data):
        x, y, z, confidence = landmark
        message = f"/mediapipe/pose,{i},{x},{y},{z},{confidence}"
        osc_out.sendOSC(message)

def send_hand_data(hand_data):
    # Send hand landmarks via OSC
    osc_out = op('osc_out')
    
    for hand_id, hand_landmarks in enumerate(hand_data):
        for i, landmark in enumerate(hand_landmarks):
            x, y, z, confidence = landmark
            message = f"/mediapipe/hands,{hand_id},{i},{x},{y},{z},{confidence}"
            osc_out.sendOSC(message)

def send_face_data(face_data):
    # Send face landmarks via OSC
    osc_out = op('osc_out')
    
    for i, landmark in enumerate(face_data):
        x, y, z, confidence = landmark
        message = f"/mediapipe/face,{i},{x},{y},{z},{confidence}"
        osc_out.sendOSC(message)
```

## Plugin-Specific OSC Message Formats

### Expected Plugin Output Formats
The MediaPipe TouchDesigner plugin may output data in different formats. Here are common formats:

#### Format 1: Individual Landmark Messages
```
/mediapipe/pose,{landmark_id},{x},{y},{z},{confidence}
/mediapipe/hands,{hand_id},{landmark_id},{x},{y},{z},{confidence}
/mediapipe/face,{landmark_id},{x},{y},{z},{confidence}
```

#### Format 2: Batch Messages
```
/mediapipe/pose_batch,{landmark_count},{x1},{y1},{z1},{conf1},{x2},{y2},{z2},{conf2},...
/mediapipe/hands_batch,{hand_count},{landmark_count},{hand1_data},{hand2_data},...
```

#### Format 3: Plugin-Specific Format
```
/mediapipe/plugin/pose,{landmark_id},{x},{y},{z},{confidence}
/mediapipe/plugin/hands,{hand_id},{landmark_id},{x},{y},{z},{confidence}
```

## Updated Unity Script for Plugin Compatibility

If the plugin uses a different message format, you may need to modify the `MediaPipeOSCReceiver.cs` script:

```csharp
// Add these methods to handle different plugin formats
void ProcessPluginPoseBatch(Message message)
{
    // Handle batch pose data from plugin
    if (message.values.Length >= 2)
    {
        int landmarkCount = (int)message.values[0];
        int dataIndex = 1;
        
        for (int i = 0; i < landmarkCount && dataIndex + 3 < message.values.Length; i++)
        {
            float x = (float)message.values[dataIndex];
            float y = (float)message.values[dataIndex + 1];
            float z = (float)message.values[dataIndex + 2];
            float confidence = (float)message.values[dataIndex + 3];
            
            if (confidence >= confidenceThreshold)
            {
                Vector3 position = new Vector3(x, y, z);
                position = TransformCoordinates(position);
                
                if (poseLandmarksToBones.ContainsKey(i))
                {
                    string boneName = poseLandmarksToBones[i];
                    UpdateBonePosition(boneName, position, i);
                }
            }
            
            dataIndex += 4;
        }
    }
}

void ProcessPluginSpecific(Message message)
{
    // Handle plugin-specific message format
    if (message.address.StartsWith("/mediapipe/plugin/"))
    {
        // Extract data based on plugin format
        // Implementation depends on specific plugin output
    }
}
```

## Plugin Configuration Tips

### 1. Performance Optimization
- **Use GPU acceleration** if available
- **Reduce model complexity** for better performance
- **Lower detection confidence** for more responsive tracking
- **Enable only needed features** (pose, hands, face)

### 2. Quality Settings
- **Detection Confidence**: 0.5-0.7 (balance between accuracy and responsiveness)
- **Tracking Confidence**: 0.5-0.7 (maintains tracking during movement)
- **Model Complexity**: Choose based on performance needs

### 3. Camera Setup
- **Good lighting** for better detection
- **Clear background** to avoid false detections
- **Proper camera positioning** (full body visible)

## Testing with Plugin

### 1. Plugin Output Verification
1. Check plugin's built-in visualization
2. Verify landmark detection is working
3. Test OSC output (if built-in)

### 2. Unity Integration Test
1. Use `OSCTestSender.cs` to test communication
2. Verify Unity receives plugin data
3. Check avatar responds to movements

### 3. Performance Monitoring
1. Monitor TouchDesigner frame rate
2. Check Unity performance
3. Adjust plugin settings as needed

## Troubleshooting Plugin Issues

### Common Plugin Problems

1. **Plugin not detecting poses**
   - Check camera input
   - Verify lighting conditions
   - Adjust detection confidence
   - Ensure plugin is properly installed

2. **Poor performance**
   - Enable GPU acceleration
   - Reduce model complexity
   - Lower frame rate
   - Close other applications

3. **Incorrect landmark positions**
   - Check coordinate system settings
   - Verify camera calibration
   - Adjust plugin parameters

4. **OSC communication issues**
   - Verify plugin OSC output format
   - Check Unity OSC receiver settings
   - Test with simple messages first

## Advanced Plugin Features

### 1. Multiple Person Tracking
If the plugin supports multiple person tracking:
```python
# Extract multiple person data
def send_multiple_poses(poses_data):
    for person_id, pose_data in enumerate(poses_data):
        for i, landmark in enumerate(pose_data):
            x, y, z, confidence = landmark
            message = f"/mediapipe/pose,{person_id},{i},{x},{y},{z},{confidence}"
            osc_out.sendOSC(message)
```

### 2. Plugin-Specific Features
- **Segmentation masks** for background removal
- **3D mesh generation** for detailed tracking
- **Custom landmark sets** for specific applications
- **Recording/playback** capabilities

## Support and Resources

### Plugin Documentation
- Check the plugin's GitHub repository for detailed documentation
- Look for example projects and tutorials
- Join the plugin's community for support

### Unity Integration
- Use the provided `MediaPipeOSCReceiver.cs` script
- Modify message processing for plugin-specific formats
- Test thoroughly with your specific setup

### Performance Optimization
- Profile both TouchDesigner and Unity performance
- Adjust settings based on your hardware capabilities
- Consider using multiple computers for heavy processing

This setup should provide excellent performance and ease of use compared to manual MediaPipe integration! 