using Newtonsoft.Json;
using RestSharp;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;

namespace WinGister.Shell
{

    /// <summary>
    /// The WinGisterExtension Class.
    /// </summary>
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.Directory)]
    [COMServerAssociation(AssociationType.Class, @"Directory\Background")]
    [COMServerAssociation(AssociationType.Class, @"DesktopBackground")]
    public class WinGisterExtension : SharpContextMenu
    {

        private string _BaseGitHubApiUrl = "https://api.github.com";
        private ContextMenuStrip _ContextMenu = new ContextMenuStrip();
        private DateTime _LastRefresh = DateTime.MinValue;


        #region : CONTEXT MENU OVERRIDES

        // <summary>
        // Determines whether the menu item can be shown for the selected item.
        // </summary>
        // <returns><c>true</c> if item can be shown for the selected item for this instance.; otherwise, <c>false</c>.</returns>
        protected override bool CanShowMenu()
        {
            /* REFRESH THE MENU IF OUR CONTEXT MENU HAS BEEN LOST OR IF THE LAST REFRESH WAS OVER 15 MINUTES AGO */
            if ((_ContextMenu == null) || (DateTime.Compare(DateTime.Now.AddMinutes(-15), _LastRefresh) > 0) || (_ContextMenu.Items.Count == 0))
                RefreshMenu();

            return true;
        }

        // <summary>
        // Creates the context menu.
        // </summary>
        // <returns>The context menu for the shell context menu.</returns>
        protected override ContextMenuStrip CreateMenu()
        {
            /* PREVENT EXCESSIVE API CALLS, LIMIT TO EVERY 15 MINUTES */
            if ((_ContextMenu != null) && (_ContextMenu.Items.Count > 0) && (DateTime.Compare(DateTime.Now.AddMinutes(-15), _LastRefresh) < 0))
                return _ContextMenu;
            
            _ContextMenu.Items.Clear();

            ToolStripMenuItem MainMenu;
            MainMenu = new ToolStripMenuItem
            {
                Text = "Paste from GitHub Gists",
                Image = Properties.Resources.GitHubIcon
            };

            string user = GetUserFromConfig();

            GistResponse gistResp = GetGistList(user);
            if (gistResp.Success)
            { 

                foreach (var gist in gistResp.Gists)
                {
                    ToolStripMenuItem gistMenu = new ToolStripMenuItem
                    {
                        Text = gist.Description,
                        Image = Properties.Resources.BranchIcon
                    };

                    foreach (var file in gist.Files)
                    {
                        ToolStripMenuItem gistFile = new ToolStripMenuItem
                        {
                            Text = file.FileName,
                            Image = Properties.Resources.GistIcon,
                            Tag = file.RawUrl
                        };

                        gistFile.Click += (sender, args) => DownloadGist(sender, args);
                        gistMenu.DropDownItems.Add(gistFile);
                    }

                    MainMenu.DropDownItems.Add(gistMenu);
                }
            }
            else
            {
                MainMenu.DropDownItems.Add(new ToolStripMenuItem
                {
                    Text = gistResp.Message,
                    Image = Properties.Resources.ErrorIcon
                });
            }

            _ContextMenu.Items.Clear();
            _ContextMenu.Items.Add(MainMenu);
            _LastRefresh = DateTime.Now;
            return _ContextMenu;
        }

        #endregion


        #region : POPULATE CONTEXT MENU

        // <summary>
        // Refreshes the context menu. 
        // </summary>
        private void RefreshMenu()
        {
            _ContextMenu.Dispose();
            _ContextMenu = CreateMenu();
        }

        /// <summary>
        /// The click event for the Gist File Item.
        /// </summary>
        /// <param name="sender">The ToolStripMenuItem that triggered the event</param>
        /// <param name="e">The event arguments</param>
        protected void DownloadGist(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            var url = item.Tag.ToString();

            var fileContents = GetGist(url);

            if (fileContents == null)
            {
                MessageBox.Show("There was an un-expected error while retrieving your file...", "WinGitter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            string selectedPath;
            if (SelectedItemPaths.Count() > 0)
                selectedPath = SelectedItemPaths.First();
            else
                selectedPath = this.FolderPath;

            FileAttributes attr = File.GetAttributes(selectedPath);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                // DOES FILE ALREADY EXIST?
                var filePath = Path.Combine(selectedPath, item.Text);
                if (File.Exists(filePath))
                {
                    DialogResult dialogResult = MessageBox.Show("Would you like to overwrite the existing file?", "WinGitter", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dialogResult == DialogResult.No)
                    {
                        return;
                    }
                }

                File.WriteAllText(filePath, fileContents);
            }
            else
            {
                MessageBox.Show("Unexpected bahavior...", "WinGitter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion


        #region : HANDLE GITHUB API AND RESPONSES

        /// <summary>
        /// Makes a call to the GitHub API and returns a list of Gists for the specified user.
        /// </summary>
        /// <param name="publicUsername">The public GitHub username to retrieve Gists for</param>
        /// <returns>A GistResponse object</returns>
        public GistResponse GetGistList(string publicUsername)
        {
            if (string.IsNullOrEmpty(publicUsername))
                return new GistResponse()
                {
                    Success = false,
                    Message = "No username supplied...",
                    Gists = null
                };

            var client = new RestClient(_BaseGitHubApiUrl);

            var request = new RestRequest("users/{username}/gists", Method.GET);
            request.AddUrlSegment("username", publicUsername);

            IRestResponse response = client.Execute(request);
            var content = response.Content;

            if (response.StatusCode != System.Net.HttpStatusCode.OK || string.IsNullOrEmpty(content))
                return new GistResponse()
                {
                    Success = false,
                    Message = "Internal error...",
                    Gists = null
                };

            List<Gist> gists = ParseGistList(content);

            if (gists == null)
                return new GistResponse()
                {
                    Success = false,
                    Message = "Internal error...",
                    Gists = null
                };

            if (gists.Count == 0)
                return new GistResponse()
                {
                    Success = false,
                    Message = "No gists available...",
                    Gists = null
                };

            return new GistResponse()
            {
                Success = true,
                Message = "Success",
                Gists = gists
            };
        }

        /// <summary>
        /// Downloads a GitHub raw file and returns the contents as a string.
        /// </summary>
        /// <param name="gistRawUrl">The full GitHub url for the raw file</param>
        /// <returns>The file content as a string, <c>NULL</c> on error</returns>
        public string GetGist(string gistRawUrl)
        {
            var client = new RestClient(gistRawUrl);

            var request = new RestRequest(Method.GET);

            IRestResponse response = client.Execute(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                return null;

            return response.Content;
        }

        /// <summary>
        /// Parses JSON from a GitHub Gist API call and returns a serialized collection.
        /// </summary>
        /// <param name="json">The raw json to parse</param>
        /// <returns>A collection of <c>Gist</c> objects</returns>
        private List<Gist> ParseGistList(string json)
        {
            var gists = new List<Gist>();

            try
            {
                dynamic jsonResponse = JsonConvert.DeserializeObject(json);

                foreach (var r in jsonResponse)
                {
                    Gist gist = new Gist();
                    gist.Files = new List<GistFile>();

                    gist.Description = r.description;
                    foreach (var f in r.files)
                    {
                        GistFile file = new GistFile();
                        file.FileName = f.Name;
                        file.RawUrl = f.Value.raw_url;

                        if (!string.IsNullOrEmpty(file.FileName) && !string.IsNullOrEmpty(file.RawUrl))
                            gist.Files.Add(file);
                    }

                    if (!string.IsNullOrEmpty(gist.Description) && gist.Files.Count > 0)
                        gists.Add(gist);
                }
            }
            catch
            {
                gists = null;
            }

            return gists;
        }

        /// <summary>
        /// Retrieves a GitHub username from the configuration.
        /// </summary>
        /// <returns>The GitHub username</returns>
        private string GetUserFromConfig()
        {
            try
            {
                var cfolder = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).Directory.FullName;
                var cpath = Path.Combine(cfolder, "WinGister.Shell.config.xml");
                XmlDocument xdoc = new XmlDocument();
                xdoc.Load(cpath);

                XmlNode node = xdoc.SelectSingleNode("//Config/GitHubUser");
                return node.InnerText; 
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

    }

}
