using UnityEngine;
using System.IO;
using AudioSynthesis;
using System.Collections;
using UnityEngine.Networking;

namespace UnityMidi
{
    [System.Serializable]
    public class StreamingAssetResouce : IResource
    {
        [SerializeField] string streamingAssetPath;

        private byte[] _results;

        public StreamingAssetResouce() { }
        public StreamingAssetResouce(string path) {
            streamingAssetPath = path;

        }

        public bool ReadAllowed()
        {
            return true;
        }

        public bool WriteAllowed()
        {
            return false;
        }

        public bool DeleteAllowed()
        {
            return false;
        }

        public string GetName()
        {
            return Path.GetFileName(streamingAssetPath);
        }

        public Stream OpenResourceForRead()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new MemoryStream(_results);
#else
            return File.OpenRead(Path.Combine(Application.streamingAssetsPath, streamingAssetPath));
#endif
        }

        public IEnumerator ReadResourceRoutine()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string path = Path.Combine(Application.streamingAssetsPath, streamingAssetPath);
            Debug.Log("Reading resource with webreq from path: " + path);
            using (UnityWebRequest www = UnityWebRequest.Get(path))
            {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.LogError(www.error);
                }
                else
                {
                    _results = www.downloadHandler.data;
                }
            }

#endif
            yield break;
        }

        Stream IResource.OpenResourceForWrite()
        {
            throw new System.NotImplementedException();
        }

        void IResource.DeleteResource()
        {
            throw new System.NotImplementedException();
        }
    }
}
