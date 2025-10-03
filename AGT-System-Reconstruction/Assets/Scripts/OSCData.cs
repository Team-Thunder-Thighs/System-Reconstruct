using System.Collections.Generic;
using UnityEngine;
using uOSC;

namespace AgtOscData
{

    /// <summary>
    /// Simple data container that can hold any combination of named parameters
    /// </summary>
    [System.Serializable]
    public class OSCMessage
    {
        public string address;
        public Dictionary<string, object> data;

        public OSCMessage(string address)
        {
            this.address = address;
            this.data = new Dictionary<string, object>();
        }

        // Fluent interface for easy building
        public OSCMessage Set(string key, object value)
        {
            data[key] = value;
            return this;
        }

        // Type-safe getters with defaults
        public int GetInt(string key, int defaultValue = 0)
        {
            if (data.ContainsKey(key) && data[key] is int intVal)
                return intVal;
            if (data.ContainsKey(key) && data[key] is float floatVal)
                return (int)floatVal; // Auto-convert float to int
            return defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (data.ContainsKey(key) && data[key] is float floatVal)
                return floatVal;
            if (data.ContainsKey(key) && data[key] is int intVal)
                return (float)intVal; // Auto-convert int to float
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (data.ContainsKey(key) && data[key] is bool boolVal)
                return boolVal;
            if (data.ContainsKey(key) && data[key] is int intVal)
                return intVal != 0; // Auto-convert int to bool
            return defaultValue;
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (data.ContainsKey(key) && data[key] is string stringVal)
                return stringVal;
            if (data.ContainsKey(key))
                return data[key].ToString(); // Auto-convert any type to string
            return defaultValue;
        }

        public Vector2 GetVector2(string key, Vector2 defaultValue = default)
        {
            if (data.ContainsKey(key) && data[key] is Vector2 vec2Val)
                return vec2Val;
            return defaultValue;
        }

        // Check if key exists
        public bool Has(string key) => data.ContainsKey(key);

        // Get all keys
        public IEnumerable<string> Keys => data.Keys;

        // Convert to object array for sending (preserves order if keys provided)
        public object[] ToArray(params string[] keyOrder)
        {
            if (keyOrder.Length == 0)
            {
                // Return all values in dictionary order
                var values = new List<object>();
                foreach (var kvp in data)
                {
                    values.Add(kvp.Value);
                }

                return values.ToArray();
            }
            else
            {
                // Return values in specified order
                var values = new List<object>();
                foreach (string key in keyOrder)
                {
                    if (data.ContainsKey(key))
                    {
                        values.Add(data[key]);
                    }
                }

                return values.ToArray();
            }
        }

        public override string ToString()
        {
            var pairs = new List<string>();
            foreach (var kvp in data)
            {
                pairs.Add($"{kvp.Key}:{kvp.Value}");
            }

            return $"{address}({string.Join(", ", pairs)})";
        }
    }

    /// <summary>
    /// Simple data type definitions
    /// </summary>
    public static class DataTypes
    {
        // Hand tracking data
        public static OSCMessage HandData(int fingers, float x, float y, float confidence = 1f)
        {
            return new OSCMessage("/hand/data")
                .Set("fingers", fingers)
                .Set("x", x)
                .Set("y", y)
                .Set("confidence", confidence);
        }

        // UI element data
        public static OSCMessage UIElement(string id, int requiredFingers, Vector2 position, Vector2 size)
        {
            return new OSCMessage("/ui/element")
                .Set("id", id)
                .Set("required_fingers", requiredFingers)
                .Set("x", position.x)
                .Set("y", position.y)
                .Set("width", size.x)
                .Set("height", size.y);
        }

        // Interaction result
        public static OSCMessage InteractionResult(string elementId, bool success, int actualFingers,
            int requiredFingers)
        {
            return new OSCMessage("/interaction/result")
                .Set("element_id", elementId)
                .Set("success", success)
                .Set("actual_fingers", actualFingers)
                .Set("required_fingers", requiredFingers);
        }

        // Game state
        public static OSCMessage GameState(int score, int level, int correct, int wrong)
        {
            return new OSCMessage("/game/state")
                .Set("score", score)
                .Set("level", level)
                .Set("correct", correct)
                .Set("wrong", wrong);
        }

        // Audio event
        public static OSCMessage AudioEvent(string soundName, float volume = 1f)
        {
            return new OSCMessage("/audio/event")
                .Set("sound", soundName)
                .Set("volume", volume);
        }

        // Generic trigger
        public static OSCMessage Trigger(string triggerName, float intensity = 1f)
        {
            return new OSCMessage("/trigger")
                .Set("name", triggerName)
                .Set("intensity", intensity);
        }
    }

    /// <summary>
    /// Simple message handler registry
    /// </summary>
    public class MessageHandler
    {
        private Dictionary<string, System.Action<OSCMessage>> handlers =
            new Dictionary<string, System.Action<OSCMessage>>();

        public void Register(string address, System.Action<OSCMessage> handler)
        {
            handlers[address] = handler;
        }

        public void Unregister(string address)
        {
            handlers.Remove(address);
        }

        public void Process(Message rawMessage)
        {
            if (handlers.ContainsKey(rawMessage.address))
            {
                // Convert raw uOSC message to our simple format
                var oscMessage = FromRawMessage(rawMessage);
                handlers[rawMessage.address](oscMessage);
            }
        }

        // Convert uOSC Message to our OSCMessage format
        public static OSCMessage FromRawMessage(Message rawMessage, params string[] paramNames)
        {
            var oscMessage = new OSCMessage(rawMessage.address);

            if (rawMessage.values != null)
            {
                for (int i = 0; i < rawMessage.values.Length; i++)
                {
                    string key = (i < paramNames.Length) ? paramNames[i] : $"param_{i}";
                    oscMessage.Set(key, rawMessage.values[i]);
                }
            }

            return oscMessage;
        }
    }


    /// <summary>
    /// Simple helper for common OSC operations
    /// </summary>
    public static class OscHelper
    {
        /// <summary>
        /// Send an OSC message using the simple system
        /// </summary>
        public static void Send(OSCMessage message, params string[] paramOrder)
        {
            if (OSCManager.Instance == null)
            {
                Debug.LogWarning("[SimpleOSC] OSCManager not available");
                return;
            }

            var values = message.ToArray(paramOrder);
            OSCManager.Instance.SendMessage(message.address, values);
        }

        /// <summary>
        /// Quick send with fluent interface
        /// </summary>
        public static void Send(string address, System.Action<OSCMessage> buildMessage)
        {
            var message = new OSCMessage(address);
            buildMessage(message);
            Send(message);
        }

        /// <summary>
        /// Create message handler that automatically converts raw messages
        /// </summary>
        public static void BindHandler(string address, System.Action<OSCMessage> handler,
            params string[] paramNames)
        {
            if (OSCManager.Instance == null)
            {
                Debug.LogWarning("[SimpleOSC] OSCManager not available");
                return;
            }

            OSCManager.Instance.BindReceiver(address, (rawMessage) =>
            {
                var oscMessage = MessageHandler.FromRawMessage(rawMessage, paramNames);
                handler(oscMessage);
            });
        }
    }

}