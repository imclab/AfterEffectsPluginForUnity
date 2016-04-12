using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace UTJ
{
    [CustomEditor(typeof(AfterEffectsFx))]
    public class AfterEffectsFxEditor : Editor
    {

        void OnEnable()
        {
        }


        static string MakePathRelative(string path)
        {
            Uri pathToAssets = new Uri(Application.streamingAssetsPath + "/");
            return pathToAssets.MakeRelativeUri(new Uri(path)).ToString();
        }

        bool SelectPlugin()
        {
            var t = target as AfterEffectsFx;
            var path = EditorUtility.OpenFilePanel("Select OpenToonz plugin", Application.streamingAssetsPath + "OpenToonzPlugin", "aex");
            if(path == "")
            {
                return false;
            }

            AfterEffectsFx.PluginPath ppath;
            if (path.IndexOf(Application.streamingAssetsPath) == -1)
            {
                ppath = new AfterEffectsFx.PluginPath
                {
                    m_root = AfterEffectsFx.PluginPath.Root.Absolute,
                    m_leaf = path,
                };
            }
            else
            {
                ppath = new AfterEffectsFx.PluginPath
                {
                    m_root = AfterEffectsFx.PluginPath.Root.StreamingAssetsPath,
                    m_leaf = MakePathRelative(path),
                };
            }

            Undo.RecordObject(target, "Changed Plugin");

            t.pluginPath = ppath;
            return true;
        }

        public override void OnInspectorGUI()
        {
            //DrawDefaultInspector();
            //return;

            var t = target as AfterEffectsFx;
            const float width = 20.0f;
            const float label_width_1c = 16.0f;
            bool repaint = false;

            // "Select Plugin" button
            if (GUILayout.Button("Select Plugin", GUILayout.MinWidth(width)))
            {
                repaint = SelectPlugin();
            }

            EditorGUILayout.Space();

            // params
            GUILayout.Label("Params:");
            var cparams = t.pluginParams;
            if (cparams != null)
            {
                var options = GUILayout.MinWidth(width);

                foreach (var p in cparams)
                {
                    if(p == null) { continue; }
                    var type = p.GetType();

                    // bool
                    if (type == typeof(AEFxBoolParam))
                    {
                        var vp = p as AEFxBoolParam;
                        EditorGUI.BeginChangeCheck();

                        var v = EditorGUILayout.Toggle(p.name, vp.value.value != 0, options);

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(target, "Changed Param Value");
                            vp.value.value = v ? 1 : 0;
                            vp.Sanitize();
                            repaint = true;
                        }
                    }
                    // int
                    else if (type == typeof(AEFxIntParam))
                    {
                        var vp = p as AEFxIntParam;
                        EditorGUI.BeginChangeCheck();

                        var v = EditorGUILayout.IntField(p.name, vp.value.value, options);

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(target, "Changed Param Value");
                            vp.value.value = v;
                            vp.Sanitize();
                            repaint = true;
                        }
                    }
                    // double
                    else if(type == typeof(AEFxDoubleParam))
                    {
                        var vp = p as AEFxDoubleParam;
                        EditorGUI.BeginChangeCheck();

                        var v = EditorGUILayout.DoubleField(p.name, vp.value.value, options);

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(target, "Changed Param Value");
                            vp.value.value = v;
                            vp.Sanitize();
                            repaint = true;
                        }
                    }
                    // point2D
                    else if (type == typeof(AEFxPoint2DParam))
                    {
                        var vp = p as AEFxPoint2DParam;
                        EditorGUI.BeginChangeCheck();

                        EditorGUIUtility.labelWidth = 1.0f;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(p.name, options);
                        EditorGUIUtility.labelWidth = label_width_1c;
                        var x = EditorGUILayout.IntField("X", vp.value.value.x, options);
                        var y = EditorGUILayout.IntField("Y", vp.value.value.y, options);
                        EditorGUILayout.EndHorizontal();
                        EditorGUIUtility.labelWidth = 0.0f;

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(target, "Changed Param Value");
                            vp.value.value.x = x;
                            vp.value.value.y = y;
                            repaint = true;
                        }
                    }
                    // point3D
                    else if (type == typeof(AEFxPoint3DParam))
                    {
                        var vp = p as AEFxPoint3DParam;
                        EditorGUI.BeginChangeCheck();

                        EditorGUIUtility.labelWidth = 1.0f;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(p.name, options);
                        EditorGUIUtility.labelWidth = label_width_1c;
                        var x = EditorGUILayout.DoubleField("X", vp.value.value.x, options);
                        var y = EditorGUILayout.DoubleField("Y", vp.value.value.y, options);
                        var z = EditorGUILayout.DoubleField("Z", vp.value.value.z, options);
                        EditorGUILayout.EndHorizontal();
                        EditorGUIUtility.labelWidth = 0.0f;

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(target, "Changed Param Value");
                            vp.value.value.x = x;
                            vp.value.value.y = y;
                            vp.value.value.z = z;
                            repaint = true;
                        }
                    }
                    // color
                    else if (type == typeof(AEFxColorParam))
                    {
                        var vp = p as AEFxColorParam;
                        EditorGUI.BeginChangeCheck();

                        EditorGUILayout.LabelField(p.name, options);
                        EditorGUIUtility.labelWidth = label_width_1c;
                        EditorGUILayout.BeginHorizontal();
                        var r = EditorGUILayout.IntField("R", vp.value.value.r, options);
                        var g = EditorGUILayout.IntField("G", vp.value.value.g, options);
                        var b = EditorGUILayout.IntField("B", vp.value.value.b, options);
                        var a = EditorGUILayout.IntField("A", vp.value.value.a, options);
                        EditorGUILayout.EndHorizontal();
                        EditorGUIUtility.labelWidth = 0.0f;

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(target, "Changed Param Value");
                            vp.value.value.r = (byte)r;
                            vp.value.value.g = (byte)g;
                            vp.value.value.b = (byte)b;
                            vp.value.value.a = (byte)a;
                            repaint = true;
                        }
                    }
                }
            }

            if(repaint && !Application.isPlaying)
            {
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }
    }
}