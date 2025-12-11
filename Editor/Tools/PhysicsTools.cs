using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for physics operations like raycasting and force application.
    /// </summary>
    [McpToolProvider]
    public class PhysicsTools
    {
        [McpTool("unity_raycast", "Cast a ray and return hit information", typeof(RaycastArgs))]
        public static object Raycast(string argsJson)
        {
            var args = JsonUtility.FromJson<RaycastArgs>(argsJson);
            
            if (args?.origin == null)
                return new { error = "origin parameter is required" };
            if (args?.direction == null)
                return new { error = "direction parameter is required" };
            
            Vector3 origin = new Vector3(args.origin.x, args.origin.y, args.origin.z);
            Vector3 direction = new Vector3(args.direction.x, args.direction.y, args.direction.z).normalized;
            float maxDistance = args.maxDistance > 0 ? args.maxDistance : Mathf.Infinity;
            
            RaycastHit hit;
            bool didHit;
            
            if (!string.IsNullOrEmpty(args.layerMask))
            {
                int layer = LayerMask.GetMask(args.layerMask.Split(','));
                didHit = Physics.Raycast(origin, direction, out hit, maxDistance, layer);
            }
            else
            {
                didHit = Physics.Raycast(origin, direction, out hit, maxDistance);
            }
            
            if (!didHit)
            {
                return new RaycastResult
                {
                    hit = false,
                    origin = $"({origin.x:F2}, {origin.y:F2}, {origin.z:F2})",
                    direction = $"({direction.x:F2}, {direction.y:F2}, {direction.z:F2})",
                    maxDistance = maxDistance
                };
            }
            
            return new RaycastResult
            {
                hit = true,
                origin = $"({origin.x:F2}, {origin.y:F2}, {origin.z:F2})",
                direction = $"({direction.x:F2}, {direction.y:F2}, {direction.z:F2})",
                maxDistance = maxDistance,
                hitPoint = $"({hit.point.x:F2}, {hit.point.y:F2}, {hit.point.z:F2})",
                hitNormal = $"({hit.normal.x:F2}, {hit.normal.y:F2}, {hit.normal.z:F2})",
                hitDistance = hit.distance,
                hitObjectName = hit.collider.gameObject.name,
                hitObjectPath = GetGameObjectPath(hit.collider.gameObject),
                hitColliderType = hit.collider.GetType().Name
            };
        }

        [McpTool("unity_overlap_sphere", "Find all colliders within a sphere", typeof(OverlapSphereArgs))]
        public static object OverlapSphere(string argsJson)
        {
            var args = JsonUtility.FromJson<OverlapSphereArgs>(argsJson);
            
            if (args?.center == null)
                return new { error = "center parameter is required" };
            
            float radius = args.radius > 0 ? args.radius : 1f;
            Vector3 center = new Vector3(args.center.x, args.center.y, args.center.z);
            int maxResults = args.maxResults > 0 ? args.maxResults : 20;
            
            Collider[] colliders;
            
            if (!string.IsNullOrEmpty(args.layerMask))
            {
                int layer = LayerMask.GetMask(args.layerMask.Split(','));
                colliders = Physics.OverlapSphere(center, radius, layer);
            }
            else
            {
                colliders = Physics.OverlapSphere(center, radius);
            }
            
            var results = new List<OverlapResult>();
            for (int i = 0; i < Mathf.Min(colliders.Length, maxResults); i++)
            {
                var col = colliders[i];
                results.Add(new OverlapResult
                {
                    name = col.gameObject.name,
                    path = GetGameObjectPath(col.gameObject),
                    colliderType = col.GetType().Name,
                    distance = Vector3.Distance(center, col.transform.position)
                });
            }
            
            return new OverlapSphereResult
            {
                center = $"({center.x:F2}, {center.y:F2}, {center.z:F2})",
                radius = radius,
                totalFound = colliders.Length,
                returnedCount = results.Count,
                colliders = results.ToArray()
            };
        }

        [McpTool("unity_add_force", "Apply force to a Rigidbody (requires Play Mode)", typeof(AddForceArgs))]
        public static object AddForce(string argsJson)
        {
            var args = JsonUtility.FromJson<AddForceArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required" };
            if (args?.force == null)
                return new { error = "force parameter is required" };
            
            if (!EditorApplication.isPlaying)
                return new { error = "AddForce only works in Play Mode" };
            
            var go = GameObject.Find(args.path);
            if (go == null)
                return new { error = $"GameObject not found: {args.path}" };
            
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                return new { error = $"No Rigidbody component on: {args.path}" };
            
            Vector3 force = new Vector3(args.force.x, args.force.y, args.force.z);
            
            ForceMode mode = ForceMode.Force;
            if (!string.IsNullOrEmpty(args.forceMode))
            {
                if (!Enum.TryParse(args.forceMode, true, out mode))
                {
                    return new { error = $"Invalid force mode: {args.forceMode}. Use: Force, Acceleration, Impulse, VelocityChange" };
                }
            }
            
            rb.AddForce(force, mode);
            
            return new AddForceResult
            {
                success = true,
                path = args.path,
                force = $"({force.x:F2}, {force.y:F2}, {force.z:F2})",
                forceMode = mode.ToString(),
                currentVelocity = $"({rb.linearVelocity.x:F2}, {rb.linearVelocity.y:F2}, {rb.linearVelocity.z:F2})"
            };
        }

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

        #region Data Types

        [Serializable]
        public class Vec3
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        public class RaycastArgs
        {
            [McpParam("Ray origin point {x, y, z}", Required = true)] public Vec3 origin;
            [McpParam("Ray direction {x, y, z}", Required = true)] public Vec3 direction;
            [McpParam("Maximum ray distance")] public float maxDistance;
            [McpParam("Layer mask (comma-separated layer names)")] public string layerMask;
        }

        [Serializable]
        public class RaycastResult
        {
            public bool hit;
            public string origin;
            public string direction;
            public float maxDistance;
            public string hitPoint;
            public string hitNormal;
            public float hitDistance;
            public string hitObjectName;
            public string hitObjectPath;
            public string hitColliderType;
        }

        [Serializable]
        public class OverlapSphereArgs
        {
            [McpParam("Sphere center {x, y, z}", Required = true)] public Vec3 center;
            [McpParam("Sphere radius (default 1)")] public float radius;
            [McpParam("Layer mask (comma-separated layer names)")] public string layerMask;
            [McpParam("Maximum results to return (default 20)")] public int maxResults;
        }

        [Serializable]
        public class OverlapResult
        {
            public string name;
            public string path;
            public string colliderType;
            public float distance;
        }

        [Serializable]
        public class OverlapSphereResult
        {
            public string center;
            public float radius;
            public int totalFound;
            public int returnedCount;
            public OverlapResult[] colliders;
        }

        [Serializable]
        public class AddForceArgs
        {
            [McpParam("Path to the GameObject", Required = true)] public string path;
            [McpParam("Force vector {x, y, z}", Required = true)] public Vec3 force;
            [McpParam("Force mode", EnumValues = new[] { "Force", "Acceleration", "Impulse", "VelocityChange" })] public string forceMode;
        }

        [Serializable]
        public class AddForceResult
        {
            public bool success;
            public string path;
            public string force;
            public string forceMode;
            public string currentVelocity;
        }

        #endregion
    }
}
