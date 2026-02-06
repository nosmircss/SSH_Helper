using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Parses and navigates JSON paths like "data.items[0].name".
    /// Supports read, write, delete, and existence checking.
    /// </summary>
    public static class JsonPathNavigator
    {
        /// <summary>
        /// Represents a segment in a JSON path.
        /// </summary>
        public struct PathSegment
        {
            public string Key;
            public bool IsArrayIndex;
            public int Index;
        }

        /// <summary>
        /// Parses a JSON path like "data.items[0].name" into segments.
        /// </summary>
        public static List<PathSegment> ParsePath(string path)
        {
            var segments = new List<PathSegment>();
            var current = new StringBuilder();

            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];

                if (c == '.')
                {
                    // End of property name
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment { Key = current.ToString() });
                        current.Clear();
                    }
                }
                else if (c == '[')
                {
                    // Start of array index
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment { Key = current.ToString() });
                        current.Clear();
                    }

                    // Find the closing bracket
                    int closeIdx = path.IndexOf(']', i + 1);
                    if (closeIdx > i + 1)
                    {
                        var indexStr = path.Substring(i + 1, closeIdx - i - 1);
                        if (int.TryParse(indexStr, out var index))
                        {
                            segments.Add(new PathSegment { IsArrayIndex = true, Index = index });
                        }
                        i = closeIdx; // Skip past the closing bracket
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            // Add any remaining segment
            if (current.Length > 0)
            {
                segments.Add(new PathSegment { Key = current.ToString() });
            }

            return segments;
        }

        /// <summary>
        /// Navigates a JSON path and returns the value at that path.
        /// </summary>
        public static object? Navigate(JsonNode? node, string path)
        {
            if (node == null || string.IsNullOrEmpty(path))
                return JsonUtilities.JsonNodeToValue(node);

            var current = node;
            var segments = ParsePath(path);

            foreach (var segment in segments)
            {
                if (current == null)
                    return null;

                if (segment.IsArrayIndex)
                {
                    if (current is JsonArray arr)
                    {
                        if (segment.Index >= 0 && segment.Index < arr.Count)
                        {
                            current = arr[segment.Index];
                        }
                        else
                        {
                            return null; // Index out of bounds
                        }
                    }
                    else
                    {
                        return null; // Not an array
                    }
                }
                else
                {
                    if (current is JsonObject obj)
                    {
                        if (obj.TryGetPropertyValue(segment.Key, out var propValue))
                        {
                            current = propValue;
                        }
                        else
                        {
                            return null; // Property not found
                        }
                    }
                    else
                    {
                        return null; // Not an object
                    }
                }
            }

            return JsonUtilities.JsonNodeToValue(current);
        }

        /// <summary>
        /// Checks if a JSON path exists (distinguishes between null value and missing key).
        /// </summary>
        public static bool PathExists(JsonNode node, string path)
        {
            var segments = ParsePath(path);
            JsonNode? current = node;

            foreach (var segment in segments)
            {
                if (current == null)
                    return false;

                if (segment.IsArrayIndex)
                {
                    if (current is JsonArray arr)
                    {
                        if (segment.Index < 0 || segment.Index >= arr.Count)
                            return false;
                        current = arr[segment.Index];
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    if (current is JsonObject obj)
                    {
                        if (!obj.ContainsKey(segment.Key))
                            return false;
                        current = obj[segment.Key];
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Sets a value at a JSON path, creating intermediate objects/arrays as needed.
        /// </summary>
        public static void SetAtPath(JsonNode root, string path, JsonNode? value)
        {
            var segments = ParsePath(path);
            if (segments.Count == 0)
                return;

            JsonNode? current = root;

            // Navigate to parent of target
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var segment = segments[i];
                var nextSegment = segments[i + 1];

                if (segment.IsArrayIndex)
                {
                    if (current is JsonArray arr)
                    {
                        // Extend array if needed
                        while (arr.Count <= segment.Index)
                            arr.Add(nextSegment.IsArrayIndex ? new JsonArray() : new JsonObject());
                        current = arr[segment.Index];
                    }
                    else
                    {
                        return; // Can't navigate
                    }
                }
                else
                {
                    if (current is JsonObject obj)
                    {
                        if (!obj.ContainsKey(segment.Key))
                        {
                            obj[segment.Key] = nextSegment.IsArrayIndex ? new JsonArray() : new JsonObject();
                        }
                        current = obj[segment.Key];
                    }
                    else
                    {
                        return; // Can't navigate
                    }
                }
            }

            // Set the final value
            var lastSegment = segments[segments.Count - 1];
            if (lastSegment.IsArrayIndex)
            {
                if (current is JsonArray finalArr)
                {
                    while (finalArr.Count <= lastSegment.Index)
                        finalArr.Add(null);
                    finalArr[lastSegment.Index] = value;
                }
            }
            else
            {
                if (current is JsonObject finalObj)
                {
                    finalObj[lastSegment.Key] = value;
                }
            }
        }

        /// <summary>
        /// Deletes a value at a JSON path.
        /// </summary>
        public static void DeleteAtPath(JsonNode root, string path)
        {
            var segments = ParsePath(path);
            if (segments.Count == 0)
                return;

            JsonNode? current = root;

            // Navigate to parent of target
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var segment = segments[i];

                if (segment.IsArrayIndex)
                {
                    if (current is JsonArray arr && segment.Index >= 0 && segment.Index < arr.Count)
                        current = arr[segment.Index];
                    else
                        return;
                }
                else
                {
                    if (current is JsonObject obj && obj.TryGetPropertyValue(segment.Key, out var propValue))
                        current = propValue;
                    else
                        return;
                }
            }

            // Delete the target
            var lastSegment = segments[segments.Count - 1];
            if (lastSegment.IsArrayIndex)
            {
                if (current is JsonArray finalArr && lastSegment.Index >= 0 && lastSegment.Index < finalArr.Count)
                    finalArr.RemoveAt(lastSegment.Index);
            }
            else
            {
                if (current is JsonObject finalObj)
                    finalObj.Remove(lastSegment.Key);
            }
        }

        /// <summary>
        /// Gets the JSON type name for a node.
        /// </summary>
        public static string GetNodeType(JsonNode? node)
        {
            if (node == null)
                return "null";
            if (node is JsonObject)
                return "object";
            if (node is JsonArray)
                return "array";
            if (node is JsonValue jv)
            {
                if (jv.TryGetValue<bool>(out _))
                    return "boolean";
                if (jv.TryGetValue<long>(out _) || jv.TryGetValue<double>(out _))
                    return "number";
                return "string";
            }
            return "null";
        }
    }
}
