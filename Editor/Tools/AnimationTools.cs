using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Community.Unity.MCP
{
    /// <summary>
    /// MCP tools for controlling animations and animators.
    /// </summary>
    [McpToolProvider]
    public class AnimationTools
    {
        [McpTool("unity_set_animator_parameter", "Set a parameter on an Animator component", typeof(SetAnimatorParameterArgs))]
        public static object SetAnimatorParameter(string argsJson)
        {
            var args = JsonUtility.FromJson<SetAnimatorParameterArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required" };
            if (string.IsNullOrEmpty(args?.parameterName))
                return new { error = "parameterName parameter is required" };
            
            var go = GameObject.Find(args.path);
            if (go == null)
                return new { error = $"GameObject not found: {args.path}" };
            
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return new { error = $"No Animator component on: {args.path}" };
            
            string paramType = string.IsNullOrEmpty(args.parameterType) ? "trigger" : args.parameterType.ToLower();
            
            try
            {
                switch (paramType)
                {
                    case "bool":
                        animator.SetBool(args.parameterName, args.boolValue);
                        break;
                    case "int":
                    case "integer":
                        animator.SetInteger(args.parameterName, args.intValue);
                        break;
                    case "float":
                        animator.SetFloat(args.parameterName, args.floatValue);
                        break;
                    case "trigger":
                        animator.SetTrigger(args.parameterName);
                        break;
                    default:
                        return new { error = $"Unknown parameter type: {paramType}. Use: bool, int, float, trigger" };
                }
                
                return new SetAnimatorParameterResult
                {
                    success = true,
                    path = args.path,
                    parameterName = args.parameterName,
                    parameterType = paramType
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to set parameter: {ex.Message}" };
            }
        }

        [McpTool("unity_get_animator_info", "Get information about an Animator component", typeof(GetAnimatorInfoArgs))]
        public static object GetAnimatorInfo(string argsJson)
        {
            var args = JsonUtility.FromJson<GetAnimatorInfoArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required" };
            
            var go = GameObject.Find(args.path);
            if (go == null)
                return new { error = $"GameObject not found: {args.path}" };
            
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return new { error = $"No Animator component on: {args.path}" };
            
            // Get parameters
            var parameters = new List<AnimatorParameterInfo>();
            foreach (var param in animator.parameters)
            {
                var paramInfo = new AnimatorParameterInfo
                {
                    name = param.name,
                    type = param.type.ToString()
                };
                
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        paramInfo.currentValue = animator.GetBool(param.name).ToString();
                        break;
                    case AnimatorControllerParameterType.Int:
                        paramInfo.currentValue = animator.GetInteger(param.name).ToString();
                        break;
                    case AnimatorControllerParameterType.Float:
                        paramInfo.currentValue = animator.GetFloat(param.name).ToString("F2");
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        paramInfo.currentValue = "trigger";
                        break;
                }
                
                parameters.Add(paramInfo);
            }
            
            // Get layer info
            var layers = new List<AnimatorLayerInfo>();
            for (int i = 0; i < animator.layerCount; i++)
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(i);
                layers.Add(new AnimatorLayerInfo
                {
                    index = i,
                    name = animator.GetLayerName(i),
                    weight = animator.GetLayerWeight(i),
                    currentStateHash = stateInfo.fullPathHash,
                    normalizedTime = stateInfo.normalizedTime,
                    isInTransition = animator.IsInTransition(i)
                });
            }
            
            return new GetAnimatorInfoResult
            {
                path = args.path,
                hasController = animator.runtimeAnimatorController != null,
                controllerName = animator.runtimeAnimatorController?.name ?? "None",
                isPlaying = animator.enabled && Application.isPlaying,
                speed = animator.speed,
                parameters = parameters.ToArray(),
                layers = layers.ToArray()
            };
        }

        [McpTool("unity_play_animation", "Play an animation state on an Animator", typeof(PlayAnimationArgs))]
        public static object PlayAnimation(string argsJson)
        {
            var args = JsonUtility.FromJson<PlayAnimationArgs>(argsJson);
            
            if (string.IsNullOrEmpty(args?.path))
                return new { error = "path parameter is required" };
            if (string.IsNullOrEmpty(args?.stateName))
                return new { error = "stateName parameter is required" };
            
            var go = GameObject.Find(args.path);
            if (go == null)
                return new { error = $"GameObject not found: {args.path}" };
            
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return new { error = $"No Animator component on: {args.path}" };
            
            int layer = args.layer >= 0 ? args.layer : 0;
            float normalizedTime = args.normalizedTime >= 0 ? args.normalizedTime : 0f;
            
            try
            {
                animator.Play(args.stateName, layer, normalizedTime);
                
                return new PlayAnimationResult
                {
                    success = true,
                    path = args.path,
                    stateName = args.stateName,
                    layer = layer,
                    note = EditorApplication.isPlaying ? "Animation playing" : "Animation set (enter Play Mode to see)"
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to play animation: {ex.Message}" };
            }
        }

        #region Data Types

        [Serializable]
        public class SetAnimatorParameterArgs
        {
            [McpParam("Path to the GameObject", Required = true)] public string path;
            [McpParam("Parameter name", Required = true)] public string parameterName;
            [McpParam("Parameter type", EnumValues = new[] { "bool", "int", "float", "trigger" })] public string parameterType;
            [McpParam("Bool value (for bool type)")] public bool boolValue;
            [McpParam("Int value (for int type)")] public int intValue;
            [McpParam("Float value (for float type)")] public float floatValue;
        }

        [Serializable]
        public class SetAnimatorParameterResult
        {
            public bool success;
            public string path;
            public string parameterName;
            public string parameterType;
        }

        [Serializable]
        public class GetAnimatorInfoArgs
        {
            [McpParam("Path to the GameObject", Required = true)] public string path;
        }

        [Serializable]
        public class AnimatorParameterInfo
        {
            public string name;
            public string type;
            public string currentValue;
        }

        [Serializable]
        public class AnimatorLayerInfo
        {
            public int index;
            public string name;
            public float weight;
            public int currentStateHash;
            public float normalizedTime;
            public bool isInTransition;
        }

        [Serializable]
        public class GetAnimatorInfoResult
        {
            public string path;
            public bool hasController;
            public string controllerName;
            public bool isPlaying;
            public float speed;
            public AnimatorParameterInfo[] parameters;
            public AnimatorLayerInfo[] layers;
        }

        [Serializable]
        public class PlayAnimationArgs
        {
            [McpParam("Path to the GameObject", Required = true)] public string path;
            [McpParam("Animation state name", Required = true)] public string stateName;
            [McpParam("Layer index (default 0)")] public int layer;
            [McpParam("Normalized time to start from (0-1)")] public float normalizedTime;
        }

        [Serializable]
        public class PlayAnimationResult
        {
            public bool success;
            public string path;
            public string stateName;
            public int layer;
            public string note;
        }

        #endregion
    }
}
