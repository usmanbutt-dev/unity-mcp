using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for creating and modifying GameObjects.
    /// </summary>
    [McpToolProvider]
    public class GameObjectTools
    {
        [McpTool("unity_create_gameobject", "Create a new GameObject in the scene")]
        public static object CreateGameObject(string argsJson)
        {
            var args = JsonUtility.FromJson<CreateGameObjectArgs>(argsJson);
            
            string name = string.IsNullOrEmpty(args?.name) ? "New GameObject" : args.name;
            
            GameObject go;
            
            // Create primitive if specified
            if (!string.IsNullOrEmpty(args?.primitiveType))
            {
                if (Enum.TryParse<PrimitiveType>(args.primitiveType, true, out var primitive))
                {
                    go = GameObject.CreatePrimitive(primitive);
                    go.name = name;
                }
                else
                {
                    return new { error = $"Invalid primitive type: {args.primitiveType}. Valid types: Cube, Sphere, Capsule, Cylinder, Plane, Quad" };
                }
            }
            else
            {
                go = new GameObject(name);
            }
            
            // Set parent if specified
            if (!string.IsNullOrEmpty(args?.parentPath))
            {
                var parent = GameObject.Find(args.parentPath);
                if (parent != null)
                {
                    go.transform.SetParent(parent.transform, false);
                }
                else
                {
                    return new { error = $"Parent not found: {args.parentPath}", gameObjectCreated = true, name = go.name };
                }
            }
            // Set transform if specified
            // Note: Position and rotation (0,0,0) are valid values, so we set them anyway
            // But we DON'T check for zero since JsonUtility treats unset Vec3 as (0,0,0) which is fine for position/rotation
            if (args?.position != null)
            {
                go.transform.position = new Vector3(args.position.x, args.position.y, args.position.z);
            }
            if (args?.rotation != null)
            {
                go.transform.eulerAngles = new Vector3(args.rotation.x, args.rotation.y, args.rotation.z);
            }
            // Only set scale if it has non-zero values (JsonUtility defaults to 0,0,0 which would make object invisible)
            // A scale of (0,0,0) is almost never wanted, so we skip it
            if (args?.scale != null && !IsZeroVec3(args.scale))
            {
                go.transform.localScale = new Vector3(args.scale.x, args.scale.y, args.scale.z);
            }
            
            // Register undo
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            
            // Select the new object
            Selection.activeGameObject = go;
            
            return new CreateGameObjectResult
            {
                success = true,
                name = go.name,
                path = GetGameObjectPath(go),
                instanceId = go.GetInstanceID()
            };
        }

        [McpTool("unity_delete_gameobject", "Delete a GameObject from the scene")]
        public static object DeleteGameObject(string argsJson)
        {
            var args = JsonUtility.FromJson<DeleteGameObjectArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
            {
                return new { error = "path parameter is required" };
            }
            
            var go = GameObject.Find(args.path);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.path}" };
            }
            
            string deletedName = go.name;
            string deletedPath = GetGameObjectPath(go);
            
            // Register undo
            Undo.DestroyObjectImmediate(go);
            
            return new DeleteGameObjectResult
            {
                success = true,
                deletedName = deletedName,
                deletedPath = deletedPath
            };
        }

        [McpTool("unity_set_transform", "Set the transform (position, rotation, scale) of a GameObject")]
        public static object SetTransform(string argsJson)
        {
            var args = JsonUtility.FromJson<SetTransformArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
            {
                return new { error = "path parameter is required" };
            }
            
            var go = GameObject.Find(args.path);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.path}" };
            }
            
            Undo.RecordObject(go.transform, $"Set Transform {go.name}");
            
            bool changed = false;
            
            if (args.position != null)
            {
                if (args.useLocalSpace)
                    go.transform.localPosition = new Vector3(args.position.x, args.position.y, args.position.z);
                else
                    go.transform.position = new Vector3(args.position.x, args.position.y, args.position.z);
                changed = true;
            }
            
            if (args.rotation != null)
            {
                if (args.useLocalSpace)
                    go.transform.localEulerAngles = new Vector3(args.rotation.x, args.rotation.y, args.rotation.z);
                else
                    go.transform.eulerAngles = new Vector3(args.rotation.x, args.rotation.y, args.rotation.z);
                changed = true;
            }
            
            // Only set scale if non-zero (JsonUtility defaults to 0,0,0 which would make object invisible)
            if (args.scale != null && !IsZeroVec3(args.scale))
            {
                go.transform.localScale = new Vector3(args.scale.x, args.scale.y, args.scale.z);
                changed = true;
            }
            
            return new SetTransformResult
            {
                success = true,
                path = args.path,
                changed = changed,
                newPosition = go.transform.position.ToString(),
                newRotation = go.transform.eulerAngles.ToString(),
                newScale = go.transform.localScale.ToString()
            };
        }

        [McpTool("unity_add_component", "Add a component to a GameObject")]
        public static object AddComponent(string argsJson)
        {
            var args = JsonUtility.FromJson<AddComponentArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
            {
                return new { error = "path parameter is required" };
            }
            if (string.IsNullOrEmpty(args?.componentType))
            {
                return new { error = "componentType parameter is required" };
            }
            
            var go = GameObject.Find(args.path);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.path}" };
            }
            
            // Try to find the type
            Type componentType = FindComponentType(args.componentType);
            if (componentType == null)
            {
                return new { error = $"Component type not found: {args.componentType}. Try using full type name like 'UnityEngine.Rigidbody'" };
            }
            
            // Check if component already exists (for non-multi components)
            var existing = go.GetComponent(componentType);
            if (existing != null && !AllowsMultiple(componentType))
            {
                return new { error = $"GameObject already has component: {args.componentType}", alreadyExists = true };
            }
            
            // Add the component
            var component = Undo.AddComponent(go, componentType);
            
            return new AddComponentResult
            {
                success = true,
                path = args.path,
                componentType = componentType.Name,
                fullTypeName = componentType.FullName
            };
        }

        [McpTool("unity_remove_component", "Remove a component from a GameObject")]
        public static object RemoveComponent(string argsJson)
        {
            var args = JsonUtility.FromJson<RemoveComponentArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
            {
                return new { error = "path parameter is required" };
            }
            if (string.IsNullOrEmpty(args?.componentType))
            {
                return new { error = "componentType parameter is required" };
            }
            
            var go = GameObject.Find(args.path);
            if (go == null)
            {
                return new { error = $"GameObject not found: {args.path}" };
            }
            
            Type componentType = FindComponentType(args.componentType);
            if (componentType == null)
            {
                return new { error = $"Component type not found: {args.componentType}" };
            }
            
            var component = go.GetComponent(componentType);
            if (component == null)
            {
                return new { error = $"Component not found on GameObject: {args.componentType}" };
            }
            
            // Can't remove Transform
            if (componentType == typeof(Transform))
            {
                return new { error = "Cannot remove Transform component" };
            }
            
            Undo.DestroyObjectImmediate(component);
            
            return new RemoveComponentResult
            {
                success = true,
                path = args.path,
                removedType = componentType.Name
            };
        }

        [McpTool("unity_set_component_property", "Set a property value on a component")]
        public static object SetComponentProperty(string argsJson)
        {
            var args = JsonUtility.FromJson<SetPropertyArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required" };
            if (string.IsNullOrEmpty(args?.componentType))
                return new { error = "componentType parameter is required" };
            if (string.IsNullOrEmpty(args?.propertyName))
                return new { error = "propertyName parameter is required" };
            
            var go = GameObject.Find(args.path);
            if (go == null)
                return new { error = $"GameObject not found: {args.path}" };
            
            Type componentType = FindComponentType(args.componentType);
            if (componentType == null)
                return new { error = $"Component type not found: {args.componentType}" };
            
            var component = go.GetComponent(componentType);
            if (component == null)
                return new { error = $"Component not found on GameObject: {args.componentType}" };
            
            // Use SerializedObject for undo support
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(args.propertyName);
            
            if (property == null)
            {
                return new { error = $"Property not found: {args.propertyName}" };
            }
            
            try
            {
                SetSerializedPropertyValue(property, args.value);
                serializedObject.ApplyModifiedProperties();
                
                return new SetPropertyResult
                {
                    success = true,
                    path = args.path,
                    componentType = args.componentType,
                    propertyName = args.propertyName,
                    newValue = args.value
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to set property: {ex.Message}" };
            }
        }

        #region Helper Methods

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static bool IsZeroVec3(Vec3 v)
        {
            if (v == null) return true;
            return v.x == 0 && v.y == 0 && v.z == 0;
        }

        private static Type FindComponentType(string typeName)
        {
            // Try direct lookup first
            Type type = Type.GetType(typeName);
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return type;
            
            // Try UnityEngine namespace
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return type;
            
            // Try UnityEngine.UI
            type = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return type;
            
            // Search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if ((t.Name == typeName || t.FullName == typeName) && typeof(Component).IsAssignableFrom(t))
                        {
                            return t;
                        }
                    }
                }
                catch { }
            }
            
            return null;
        }

        private static bool AllowsMultiple(Type componentType)
        {
            return Attribute.IsDefined(componentType, typeof(DisallowMultipleComponent)) == false;
        }

        private static void SetSerializedPropertyValue(SerializedProperty property, string value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = int.Parse(value);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = float.Parse(value);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = bool.Parse(value);
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = int.Parse(value);
                    break;
                case SerializedPropertyType.Color:
                    // Expect format: "r,g,b,a"
                    var colorParts = value.Split(',');
                    if (colorParts.Length >= 3)
                    {
                        property.colorValue = new Color(
                            float.Parse(colorParts[0]),
                            float.Parse(colorParts[1]),
                            float.Parse(colorParts[2]),
                            colorParts.Length > 3 ? float.Parse(colorParts[3]) : 1f
                        );
                    }
                    break;
                case SerializedPropertyType.Vector2:
                    var v2Parts = value.Split(',');
                    if (v2Parts.Length >= 2)
                        property.vector2Value = new Vector2(float.Parse(v2Parts[0]), float.Parse(v2Parts[1]));
                    break;
                case SerializedPropertyType.Vector3:
                    var v3Parts = value.Split(',');
                    if (v3Parts.Length >= 3)
                        property.vector3Value = new Vector3(float.Parse(v3Parts[0]), float.Parse(v3Parts[1]), float.Parse(v3Parts[2]));
                    break;
                default:
                    throw new NotSupportedException($"Property type {property.propertyType} is not supported");
            }
        }

        #endregion

        #region Data Types

        [Serializable]
        public class Vec3
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        public class CreateGameObjectArgs
        {
            public string name;
            public string parentPath;
            public string primitiveType; // Cube, Sphere, Capsule, Cylinder, Plane, Quad
            public Vec3 position;
            public Vec3 rotation;
            public Vec3 scale;
        }

        [Serializable]
        public class CreateGameObjectResult
        {
            public bool success;
            public string name;
            public string path;
            public int instanceId;
        }

        [Serializable]
        public class DeleteGameObjectArgs
        {
            public string path;
        }

        [Serializable]
        public class DeleteGameObjectResult
        {
            public bool success;
            public string deletedName;
            public string deletedPath;
        }

        [Serializable]
        public class SetTransformArgs
        {
            public string path;
            public Vec3 position;
            public Vec3 rotation;
            public Vec3 scale;
            public bool useLocalSpace;
        }

        [Serializable]
        public class SetTransformResult
        {
            public bool success;
            public string path;
            public bool changed;
            public string newPosition;
            public string newRotation;
            public string newScale;
        }

        [Serializable]
        public class AddComponentArgs
        {
            public string path;
            public string componentType;
        }

        [Serializable]
        public class AddComponentResult
        {
            public bool success;
            public string path;
            public string componentType;
            public string fullTypeName;
        }

        [Serializable]
        public class RemoveComponentArgs
        {
            public string path;
            public string componentType;
        }

        [Serializable]
        public class RemoveComponentResult
        {
            public bool success;
            public string path;
            public string removedType;
        }

        [Serializable]
        public class SetPropertyArgs
        {
            public string path;
            public string componentType;
            public string propertyName;
            public string value;
        }

        [Serializable]
        public class SetPropertyResult
        {
            public bool success;
            public string path;
            public string componentType;
            public string propertyName;
            public string newValue;
        }

        #endregion
    }
}
