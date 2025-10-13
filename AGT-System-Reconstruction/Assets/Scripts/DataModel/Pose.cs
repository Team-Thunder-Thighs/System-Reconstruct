using UnityEngine;

namespace BodyTracking.DataModel
{
    /// <summary>
    /// Represents a complete body pose with 33 landmarks.
    /// Each landmark is a Vector3 position in 3D space.
    /// </summary>
    [System.Serializable]
    public struct Pose
    {
        /// <summary>
        /// Fixed-size array of 33 landmark positions (0-32).
        /// Access by index or use GetLandmark/SetLandmark methods.
        /// </summary>
        [SerializeField] private Vector3[] landmarks;
        
        /// <summary>
        /// Public property to access the landmarks array
        /// </summary>
        public Vector3[] Landmarks
        {
            get
            {
                if (landmarks == null || landmarks.Length != BodyLandmarkExtensions.TotalLandmarks)
                {
                    InitializeLandmarks();
                }
                return landmarks;
            }
            set
            {
                if (value != null && value.Length == BodyLandmarkExtensions.TotalLandmarks)
                {
                    landmarks = value;
                }
                else
                {
                    Debug.LogWarning($"[Pose] Invalid landmarks array. Expected {BodyLandmarkExtensions.TotalLandmarks} elements, got {value?.Length ?? 0}");
                }
            }
        }
        
        /// <summary>
        /// Constructor - initializes landmarks array
        /// </summary>
        public Pose(bool initialize = true)
        {
            landmarks = null;
            if (initialize)
            {
                InitializeLandmarks();
            }
        }
        
        /// <summary>
        /// Constructor with landmark data
        /// </summary>
        public Pose(Vector3[] landmarkData)
        {
            landmarks = null;
            if (landmarkData != null && landmarkData.Length == BodyLandmarkExtensions.TotalLandmarks)
            {
                landmarks = landmarkData;
            }
            else
            {
                Debug.LogWarning($"[Pose] Invalid landmark data. Expected {BodyLandmarkExtensions.TotalLandmarks} elements.");
                InitializeLandmarks();
            }
        }
        
        /// <summary>
        /// Initialize landmarks array with zero vectors
        /// </summary>
        private void InitializeLandmarks()
        {
            landmarks = new Vector3[BodyLandmarkExtensions.TotalLandmarks];
            for (int i = 0; i < BodyLandmarkExtensions.TotalLandmarks; i++)
            {
                landmarks[i] = Vector3.zero;
            }
        }
        
        /// <summary>
        /// Get landmark position by enum
        /// </summary>
        public Vector3 GetLandmark(BodyLandmark landmark)
        {
            int index = landmark.GetIndex();
            if (Landmarks != null && index >= 0 && index < Landmarks.Length)
            {
                return Landmarks[index];
            }
            return Vector3.zero;
        }
        
        /// <summary>
        /// Get landmark position by index
        /// </summary>
        public Vector3 GetLandmark(int index)
        {
            if (Landmarks != null && index >= 0 && index < Landmarks.Length)
            {
                return Landmarks[index];
            }
            Debug.LogWarning($"[Pose] Invalid landmark index: {index}");
            return Vector3.zero;
        }
        
        /// <summary>
        /// Set landmark position by enum
        /// </summary>
        public void SetLandmark(BodyLandmark landmark, Vector3 position)
        {
            int index = landmark.GetIndex();
            if (Landmarks != null && index >= 0 && index < Landmarks.Length)
            {
                Landmarks[index] = position;
            }
        }
        
        /// <summary>
        /// Set landmark position by index
        /// </summary>
        public void SetLandmark(int index, Vector3 position)
        {
            if (Landmarks != null && index >= 0 && index < Landmarks.Length)
            {
                Landmarks[index] = position;
            }
            else
            {
                Debug.LogWarning($"[Pose] Invalid landmark index: {index}");
            }
        }
        
        /// <summary>
        /// Check if all landmarks are initialized (not zero)
        /// </summary>
        public bool IsValid()
        {
            if (Landmarks == null) return false;
            
            foreach (var landmark in Landmarks)
            {
                if (landmark != Vector3.zero)
                {
                    return true; // At least one landmark is set
                }
            }
            return false;
        }
        
        /// <summary>
        /// Get the center point of the pose (average of all landmarks)
        /// </summary>
        public Vector3 GetCenter()
        {
            if (Landmarks == null || Landmarks.Length == 0) return Vector3.zero;
            
            Vector3 sum = Vector3.zero;
            int validCount = 0;
            
            foreach (var landmark in Landmarks)
            {
                if (landmark != Vector3.zero)
                {
                    sum += landmark;
                    validCount++;
                }
            }
            
            return validCount > 0 ? sum / validCount : Vector3.zero;
        }
        
        /// <summary>
        /// Copy landmarks from another pose
        /// </summary>
        public void CopyFrom(Pose other)
        {
            if (other.Landmarks != null)
            {
                Landmarks = new Vector3[BodyLandmarkExtensions.TotalLandmarks];
                System.Array.Copy(other.Landmarks, Landmarks, BodyLandmarkExtensions.TotalLandmarks);
            }
        }
        
        /// <summary>
        /// Reset all landmarks to zero
        /// </summary>
        public void Reset()
        {
            InitializeLandmarks();
        }
        
        /// <summary>
        /// Get a readable string representation of the pose
        /// </summary>
        public override string ToString()
        {
            if (Landmarks == null) return "Pose: Uninitialized";
            
            int validCount = 0;
            foreach (var landmark in Landmarks)
            {
                if (landmark != Vector3.zero) validCount++;
            }
            
            return $"Pose: {validCount}/{BodyLandmarkExtensions.TotalLandmarks} landmarks set, Center: {GetCenter()}";
        }
    }
}

