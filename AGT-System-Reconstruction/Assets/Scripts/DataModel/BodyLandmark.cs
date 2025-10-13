using UnityEngine;

namespace BodyTracking.DataModel
{
    /// <summary>
    /// Enum representing the 33 body landmarks from pose estimation.
    /// Maps descriptive names to their corresponding integer indices (0-32).
    /// Based on MediaPipe Pose landmark model.
    /// </summary>
    public enum BodyLandmark
    {
        // Face landmarks (0-10)
        Nose = 0,
        LeftEyeInner = 1,
        LeftEye = 2,
        LeftEyeOuter = 3,
        RightEyeInner = 4,
        RightEye = 5,
        RightEyeOuter = 6,
        LeftEar = 7,
        RightEar = 8,
        MouthLeft = 9,
        MouthRight = 10,
        
        // Upper body landmarks (11-22)
        LeftShoulder = 11,
        RightShoulder = 12,
        LeftElbow = 13,
        RightElbow = 14,
        LeftWrist = 15,
        RightWrist = 16,
        LeftPinky = 17,
        RightPinky = 18,
        LeftIndex = 19,
        RightIndex = 20,
        LeftThumb = 21,
        RightThumb = 22,
        
        // Lower body landmarks (23-32)
        LeftHip = 23,
        RightHip = 24,
        LeftKnee = 25,
        RightKnee = 26,
        LeftAnkle = 27,
        RightAnkle = 28,
        LeftHeel = 29,
        RightHeel = 30,
        LeftFootIndex = 31,
        RightFootIndex = 32
    }
    
    /// <summary>
    /// Helper class for BodyLandmark enum operations
    /// </summary>
    public static class BodyLandmarkExtensions
    {
        /// <summary>
        /// Total number of body landmarks
        /// </summary>
        public const int TotalLandmarks = 33;
        
        /// <summary>
        /// Get the descriptive name of a landmark
        /// </summary>
        public static string GetName(this BodyLandmark landmark)
        {
            return landmark.ToString();
        }
        
        /// <summary>
        /// Get the index of a landmark (0-32)
        /// </summary>
        public static int GetIndex(this BodyLandmark landmark)
        {
            return (int)landmark;
        }
        
        /// <summary>
        /// Get BodyLandmark from index
        /// </summary>
        public static BodyLandmark FromIndex(int index)
        {
            if (index < 0 || index >= TotalLandmarks)
            {
                Debug.LogWarning($"[BodyLandmark] Invalid index {index}. Must be between 0 and {TotalLandmarks - 1}. Returning Nose.");
                return BodyLandmark.Nose;
            }
            return (BodyLandmark)index;
        }
        
        /// <summary>
        /// Check if landmark is a face landmark (0-10)
        /// </summary>
        public static bool IsFaceLandmark(this BodyLandmark landmark)
        {
            int index = (int)landmark;
            return index >= 0 && index <= 10;
        }
        
        /// <summary>
        /// Check if landmark is an upper body landmark (11-22)
        /// </summary>
        public static bool IsUpperBodyLandmark(this BodyLandmark landmark)
        {
            int index = (int)landmark;
            return index >= 11 && index <= 22;
        }
        
        /// <summary>
        /// Check if landmark is a lower body landmark (23-32)
        /// </summary>
        public static bool IsLowerBodyLandmark(this BodyLandmark landmark)
        {
            int index = (int)landmark;
            return index >= 23 && index <= 32;
        }
        
        /// <summary>
        /// Check if landmark is on the left side of the body
        /// </summary>
        public static bool IsLeftSide(this BodyLandmark landmark)
        {
            string name = landmark.ToString();
            return name.Contains("Left");
        }
        
        /// <summary>
        /// Check if landmark is on the right side of the body
        /// </summary>
        public static bool IsRightSide(this BodyLandmark landmark)
        {
            string name = landmark.ToString();
            return name.Contains("Right");
        }
        
        /// <summary>
        /// Get the mirrored landmark (left ↔ right)
        /// Returns the same landmark if it's not paired (e.g., Nose)
        /// </summary>
        public static BodyLandmark GetMirroredLandmark(this BodyLandmark landmark)
        {
            switch (landmark)
            {
                // Face
                case BodyLandmark.LeftEyeInner: return BodyLandmark.RightEyeInner;
                case BodyLandmark.LeftEye: return BodyLandmark.RightEye;
                case BodyLandmark.LeftEyeOuter: return BodyLandmark.RightEyeOuter;
                case BodyLandmark.RightEyeInner: return BodyLandmark.LeftEyeInner;
                case BodyLandmark.RightEye: return BodyLandmark.LeftEye;
                case BodyLandmark.RightEyeOuter: return BodyLandmark.LeftEyeOuter;
                case BodyLandmark.LeftEar: return BodyLandmark.RightEar;
                case BodyLandmark.RightEar: return BodyLandmark.LeftEar;
                case BodyLandmark.MouthLeft: return BodyLandmark.MouthRight;
                case BodyLandmark.MouthRight: return BodyLandmark.MouthLeft;
                
                // Upper body
                case BodyLandmark.LeftShoulder: return BodyLandmark.RightShoulder;
                case BodyLandmark.RightShoulder: return BodyLandmark.LeftShoulder;
                case BodyLandmark.LeftElbow: return BodyLandmark.RightElbow;
                case BodyLandmark.RightElbow: return BodyLandmark.LeftElbow;
                case BodyLandmark.LeftWrist: return BodyLandmark.RightWrist;
                case BodyLandmark.RightWrist: return BodyLandmark.LeftWrist;
                case BodyLandmark.LeftPinky: return BodyLandmark.RightPinky;
                case BodyLandmark.RightPinky: return BodyLandmark.LeftPinky;
                case BodyLandmark.LeftIndex: return BodyLandmark.RightIndex;
                case BodyLandmark.RightIndex: return BodyLandmark.LeftIndex;
                case BodyLandmark.LeftThumb: return BodyLandmark.RightThumb;
                case BodyLandmark.RightThumb: return BodyLandmark.LeftThumb;
                
                // Lower body
                case BodyLandmark.LeftHip: return BodyLandmark.RightHip;
                case BodyLandmark.RightHip: return BodyLandmark.LeftHip;
                case BodyLandmark.LeftKnee: return BodyLandmark.RightKnee;
                case BodyLandmark.RightKnee: return BodyLandmark.LeftKnee;
                case BodyLandmark.LeftAnkle: return BodyLandmark.RightAnkle;
                case BodyLandmark.RightAnkle: return BodyLandmark.LeftAnkle;
                case BodyLandmark.LeftHeel: return BodyLandmark.RightHeel;
                case BodyLandmark.RightHeel: return BodyLandmark.LeftHeel;
                case BodyLandmark.LeftFootIndex: return BodyLandmark.RightFootIndex;
                case BodyLandmark.RightFootIndex: return BodyLandmark.LeftFootIndex;
                
                // Center landmarks (no mirror)
                default: return landmark;
            }
        }
    }
}

