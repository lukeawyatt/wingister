using System;
using System.Collections.Generic;

namespace WinGister.Shell
{

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class GistResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<Gist> Gists { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class Gist
    {
        public string Description { get; set; }
        public List<GistFile> Files { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class GistFile
    {
        public string FileName { get; set; }
        public string RawUrl { get; set; }
    }

}
