using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UTJ
{
    [AddComponentMenu("UTJ/AfterEffectsFx")]
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode]
    public class AfterEffectsFx : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Serializable]
        public class PluginPath
        {
            public enum Root
            {
                StreamingAssetsPath,
                Absolute,
            }

            public Root m_root;
            public string m_leaf;

            public PluginPath() { }
            public PluginPath(Root root, string leaf)
            {
                m_root = root;
                m_leaf = leaf;
            }

            public string GetPath()
            {
                if(m_leaf == null) { return ""; }
                string ret = "";
                switch (m_root)
                {
                    case Root.StreamingAssetsPath:
                        ret += Application.streamingAssetsPath;
                        break;
                    case Root.Absolute:
                        break;
                }
                if (m_leaf.Length > 0)
                {
                    if(ret.Length > 0) { ret += "/"; }
                    ret += m_leaf;
                }
                return ret;
            }

            public string GetFileName()
            {
                return Path.GetFileName(m_leaf);
            }
        }

        [SerializeField] string[] m_searchPaths;
        [SerializeField] PluginPath m_pluginPath;
        AEFxParam[] m_params;

        [SerializeField] byte[] m_serialized;

        aepAPI.aepInstance m_inst;
        aepAPI.aepPluginInfo m_plugin_info;
        aepAPI.aepLayer m_img_src;
        RenderTexture m_rt_tmp;
        RenderTexture m_rt_dst;
        string m_about;
        int m_prev_width, m_prev_height;
        bool m_began;

        int m_tw_read;
        int m_tw_write;
        int m_otp_render;



        public string[] searchPaths
        {
            get { return m_searchPaths; }
            set { m_searchPaths = value; }
        }
        public PluginPath pluginPath
        {
            get { return m_pluginPath; }
            set { m_pluginPath = value;
                ReleaseInstance();
                UpdateParamList();
            }
        }
        public AEFxParam[] pluginParams { get { return m_params; } }
        public string pluginAbout { get { return m_about; } }


        IntPtr GetTWEvent() { return TextureWriter.GetRenderEventFunc(); }
        IntPtr GetAEPEvent() { return aepAPI.GetRenderEventFunc(); }

        aepAPI.aepModule LoadModule()
        {
            foreach(var p in m_searchPaths)
            {
                aepAPI.aepAddSearchPath(p);
            }
            var mod = aepAPI.aepLoadModule(m_pluginPath.GetPath());
            return mod;
        }

        void UpdateParamList()
        {
            if(m_pluginPath == null) { return; }
            var mod = LoadModule();
            if (!mod)
            {
                ReleaseInstance();
                return;
            }

            aepAPI.aepGetPluginInfo(mod, ref m_plugin_info);
            m_about = new Regex("\\r\\n?").Replace(m_plugin_info.about, "\n");

            var inst = aepAPI.aepCreateInstance(mod);

            // update param list
            {
                int nparams = aepAPI.aepGetNumParams(inst);
                if(m_params == null || m_params.Length != nparams)
                {
                    m_params = new AEFxParam[nparams];
                }

                var pinfo = default(aepAPI.aepParamInfo);
                for (int i = 0; i < nparams; ++i)
                {
                    var paramptr = aepAPI.aepGetParam(inst, i);
                    aepAPI.aepGetParamInfo(paramptr, ref pinfo);
                    if(m_params[i] == null || m_params[i].name != pinfo.name || m_params[i].type != pinfo.type)
                    {
                        m_params[i] = aepAPI.CreateAEFxParam(paramptr);
                    }
                }
            }

            aepAPI.aepDestroyInstance(inst);
        }

        void ApplyParams()
        {
            if(!m_inst) { return; }

            // set params
            if (m_params != null)
            {
                int nparams = m_params.Length;
                for (int i = 0; i < nparams; ++i)
                {
                    if (m_params[i] == null) { continue; }
                    aepAPI.SetParamValue(aepAPI.aepGetParam(m_inst, i), m_params[i]);
                }
            }
        }


        void UpdateInputImages(Texture rt_src)
        {
            if(rt_src != null)
            {
                // copy rt_src content to memory
                if (!m_img_src)
                {
                    m_img_src = aepAPI.aepCreateLayer();
                }
                aepAPI.aepResizeLayer(m_img_src, rt_src.width, rt_src.height);

                var src_data = default(aepAPI.aepLayerData);
                aepAPI.aepGetLayerData(m_img_src, ref src_data);
                m_tw_read = TextureWriter.ReadDeferred(
                    src_data.pixels, src_data.width * src_data.height, TextureWriter.twPixelFormat.RGBAu8, rt_src, m_tw_read);
                GL.IssuePluginEvent(GetTWEvent(), m_tw_read);
            }
        }

        void ReleaseInstance()
        {
            TextureWriter.twGuardBegin();
            aepAPI.aepGuardBegin();

            // release ports & params
            aepAPI.aepDestroyLayer(m_img_src);
            m_img_src = default(aepAPI.aepLayer);

            aepAPI.aepEraseDeferredCall(m_otp_render); m_otp_render = 0;
            TextureWriter.twEraseDeferredCall(m_tw_read); m_tw_read = 0;
            TextureWriter.twEraseDeferredCall(m_tw_write); m_tw_write = 0;

            m_params = null;

            // release instance
            aepAPI.aepDestroyInstance(m_inst);
            m_inst.ptr = IntPtr.Zero;

            aepAPI.aepGuardEnd();
            TextureWriter.twGuardEnd();

            if (m_began)
            {
                aepAPI.aepEndSequence(m_inst);
                m_began = false;
            }
        }

        aepAPI.aepLayer GetInputImage()
        {
            return m_img_src;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
        }
#endif

        public void OnBeforeSerialize()
        {
            if (m_params != null)
            {
                var stream = new MemoryStream();
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, m_params);
                stream.Flush();
                m_serialized = stream.GetBuffer();
            }
        }

        public void OnAfterDeserialize()
        {
            if(m_serialized != null && m_serialized.Length > 0)
            {
                var stream = new MemoryStream(m_serialized);
                var formatter = new BinaryFormatter();
                m_params = (AEFxParam[])formatter.Deserialize(stream);
                m_serialized = null;
            }
        }


        void OnEnable()
        {
            UpdateParamList();
        }

        void OnDisable()
        {
            if (m_rt_dst != null)
            {
                m_rt_dst.Release();
                m_rt_dst = null;
            }
            if (m_rt_tmp != null)
            {
                m_rt_tmp.Release();
                m_rt_tmp = null;
            }
        }

        void OnDestroy()
        {
            ReleaseInstance();
        }

        void OnRenderImage(RenderTexture rt_src, RenderTexture rt_dst)
        {
            if (!m_inst && m_pluginPath != null)
            {
                m_inst = aepAPI.aepCreateInstance(LoadModule());
            }
            if (!m_inst)
            {
                Graphics.Blit(rt_src, rt_dst);
                return;
            }


            bool needs_blit = rt_dst == null;
            bool resolution_changed = false;
            if (m_prev_width != rt_src.width || m_prev_height != rt_src.height)
            {
                m_prev_width = rt_src.width;
                m_prev_height = rt_src.height;
                resolution_changed = true;
            }

            // rt_dst is null if this AfterEffectsFx is last post effect and camera's render target is null
            // in this case, we must allocate temporary RenderTexture, write result to it, and Blit() to destination.
            if (rt_dst == null)
            {
                if (m_rt_dst != null && resolution_changed)
                {
                    m_rt_dst.Release();
                    m_rt_dst = null;
                }
                if (m_rt_dst == null)
                {
                    m_rt_dst = new RenderTexture(rt_src.width, rt_src.height, 0, rt_src.format);
                    m_rt_dst.Create();
                }
                rt_dst = m_rt_dst;
            }

            if (m_began && resolution_changed)
            {
                aepAPI.aepEndSequence(m_inst);
                m_began = false;
            }
            if (!m_began)
            {
                aepAPI.aepBeginSequence(m_inst, rt_src.width, rt_src.height);
                m_began = true;
            }

            UpdateInputImages(rt_src);
            ApplyParams();
            aepAPI.aepSetInput(m_inst, m_img_src);
            m_otp_render = aepAPI.aepRenderDeferred(m_inst, Time.time, m_otp_render);
            GL.IssuePluginEvent(GetAEPEvent(), m_otp_render);

            var dst_data = default(aepAPI.aepLayerData);
            aepAPI.aepGetLayerData(aepAPI.aepGetDstImage(m_inst), ref dst_data);

            if (dst_data.width == rt_dst.width && dst_data.height == rt_dst.height)
            {
                m_tw_write = TextureWriter.WriteDeferred(
                    rt_dst, dst_data.pixels, dst_data.width * dst_data.height, TextureWriter.twPixelFormat.RGBAu8, m_tw_write);
                GL.IssuePluginEvent(GetTWEvent(), m_tw_write);
            }
            else
            {
                // blit & resize if size of img_dst != size of rt_dst
                if( m_rt_tmp != null && (m_rt_tmp.width != dst_data.width || m_rt_tmp.height != dst_data.height) )
                {
                    m_rt_tmp.Release();
                    m_rt_tmp = null;
                }
                if (m_rt_tmp == null)
                {
                    m_rt_tmp = new RenderTexture(dst_data.width, dst_data.height, 0, RenderTextureFormat.ARGB32);
                    m_rt_tmp.filterMode = FilterMode.Bilinear;
                    m_rt_tmp.Create();
                }
                m_tw_write = TextureWriter.WriteDeferred(
                    m_rt_tmp, dst_data.pixels, dst_data.width * dst_data.height, TextureWriter.twPixelFormat.RGBAu8, m_tw_write);
                GL.IssuePluginEvent(GetTWEvent(), m_tw_write);
                Graphics.Blit(m_rt_tmp, rt_dst);
            }

            if (needs_blit)
            {
                Graphics.Blit(rt_dst, (RenderTexture)null);
            }
        }
    }
}