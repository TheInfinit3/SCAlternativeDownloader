﻿//Copyright 2017 Hawx & Zephyr Auxiliary Services

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

//Some components of this file are based on work by NimmoG

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using static SCPatchDownloader.Utilities;


namespace SCPatchDownloader
{
    public partial class MainWindow : Form
    {
        private readonly ArrayList urlList = new ArrayList();


        struct universe
        {
            public string versionName;
            public string fileIndex;
        }

        private WebClient client;
        private string fulldir;
        readonly Stopwatch sw = new Stopwatch();



        readonly List<universe> versionList = new List<universe>();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            downloadDir.Text = Directory.GetCurrentDirectory() + "\\Star Citizen";
            toolTip_check.SetToolTip(check_nativefile, "Sorts files into public/test directorys instead of using build number. Allows for easy copy/pasting. Note that files will not be overwritten if they already exist");
            if (File.Exists("SC-URLs.txt"))
                readExistingFile();
            downloadPatchList();
        }

        async void readExistingFile()
        {
            var reader = File.OpenText("SC-URLs.txt");
            universe existingUniverse = new universe();

            while (reader.Peek() != -1)
            {
                var line = await reader.ReadLineAsync();
                urlList.Add(line);
            }
            existingUniverse.versionName = "Existing URL list";
            existingUniverse.fileIndex = "";
            versionList.Add(existingUniverse);
            relSelector.Items.Add(existingUniverse.versionName);
            reader.Close();
        }

        private void browseDir_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderDir = new FolderBrowserDialog { ShowNewFolderButton = true };
            if (folderDir.ShowDialog() == DialogResult.OK)
            {
                downloadDir.Text = folderDir.SelectedPath;

            }
        }

        private void downloadSrt_Click(object sender, EventArgs e)
        {
            butCancel.Enabled = true;
            downloadSrt.Enabled = false;
            relSelector.Enabled = false;
            releaseSelect.Enabled = false;
            downloadDir.Enabled = false;
            browseDir.Enabled = false;
            downloadGameFiles();


            async Task downloadGameFiles()
            {
                bool native = check_nativefile.Checked;
                string downloadLocation = downloadDir.Text;
                int fileNum = 1;
                int totfileNum = urlList.Count;

                DirectorySecurity security = new DirectorySecurity();
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));

                if (urlList.Count > 0 && !String.IsNullOrWhiteSpace(downloadLocation))
                {
                    try
                    {
                        foreach (string file in urlList)
                        {
                            string filename = getFileName(file);
                            label_status.Text = "Downloading file " + fileNum + " of " + totfileNum;
                            var dest = downloadLocation + getFileStructure(file, native);
                            fulldir = Path.Combine(dest, filename);
                            if (!File.Exists(fulldir))
                            {
                                Directory.CreateDirectory(dest, security);
                                using (client = new WebClient())
                                {
                                    client.DownloadProgressChanged += (send, x) => FileDownloadProgress(x.BytesReceived);
                                    client.DownloadFileCompleted += client_InstallFileCompleted;
                                    sw.Start();
                                    await client.DownloadFileTaskAsync(new Uri(file), fulldir);

                                }
                            }
                            fileNum += 1;
                            infoProg.Value = (100 * fileNum) / totfileNum;
                            sw.Reset();

                        }
                    }
                    catch (DirectoryNotFoundException)
                    {
                        MessageBox.Show("Unable to write to location, do you have permission?", "DirectoryNotFoundException",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (IOException x)
                    {
                        MessageBox.Show("Unable to write to disk. Do you have enough space? Full Exception: " + x,
                            "IOException", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    //                catch (Exception x)
                    //                {
                    //                    MessageBox.Show("Something unexpected happened and the program is unable to continue",
                    //                        "General Exception " + x, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //                    throw;
                    //                }
                }
                else
                {
                    MessageBox.Show("Please provide a valid download location", "Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                }
                if (fileNum - 1 == urlList.Count)
                {
                    File.Delete("SC-URLs.txt");
                }

            }
        }

        private void FileDownloadProgress(long bytesReceived)
        {

            label_MB.Text = $"{bytesReceived / 1024d / 1024d / sw.Elapsed.TotalSeconds:0.00} MB/s";
        }



        //handle cancelled download
        private void client_InstallFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                if (fulldir != null) File.Delete(fulldir);
                client.Dispose();
            }
        }
        //get name of downloading file
        private string getFileName(string url)
        {
            string[] parts = url.Split('/');
            string filename = parts[parts.Length - 1];
            return filename;
        }
        //get filestructure
        private string getFileStructure(string url, bool native)
        {
            string[] parts = url.Split('/');
            string filename = "";

            for (int i = 5; i < parts.Length - 1; i++)
            {
                filename += "\\" + parts[i];
            }
            return filename;
        }



        async Task downloadPatchList()
        {
            string fileLocation = "LauncherInfo.txt";
            try
            {
                using (client = new WebClient())
                {
                    client.DownloadProgressChanged += client_ProgressChanged;
                    await client.DownloadFileTaskAsync(new Uri("http://manifest.robertsspaceindustries.com/Launcher/_LauncherInfo"), fileLocation);
                    label_status.Text = "Loading Manifest...Complete";
                }
                if (File.Exists(fileLocation))
                {
                    var reader = File.OpenText(fileLocation);
                    //line in file
                    var line = reader.ReadLine();
                    var parts = line.Split('=');
                    parts = parts[1].Split(',');
                    //for each item in parts
                    foreach (var word in parts)
                    {
                        //get substring of each word
                        var versionName = word.Substring(1);
                        //get version name
                        while (!line.StartsWith(versionName + "_fileIndex"))
                        {
                            line = reader.ReadLine();
                        }
                        parts = line.Split('=');
                        var filePrefix = parts[1].Substring(1);
                        //store version name and prefix in universe object
                        universe currentUniverse;
                        currentUniverse.versionName = versionName;
                        currentUniverse.fileIndex = filePrefix;
                        versionList.Add(currentUniverse);
                        //add version names to list
                        relSelector.Items.Add(versionName);
                    }

                    if (relSelector.Items.Count > 0)
                        relSelector.SelectedIndex = 0;

                    reader.Close();
                }
                File.Delete(fileLocation);
            }
            catch (WebException)
            {
                MessageBox.Show("Connection Timed out", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void client_ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            infoProg.Value = e.ProgressPercentage;
        }

        async void releaseSelect_Click(object sender, EventArgs e)
        {
            string requestedUniverse = relSelector.SelectedItem as string;
            string universeFileList = "";
            string prefix;
            downloadSrt.Enabled = true;
            ArrayList fileList = new ArrayList();

            //file handling
            string fileName = "fileList.json";
            ArrayList baseURLS = new ArrayList();

            if (!String.IsNullOrEmpty(requestedUniverse))
            {
                foreach (var universe in versionList)
                {
                    if (requestedUniverse.Equals(universe.versionName))
                        universeFileList = universe.fileIndex;
                    else if (requestedUniverse == "Existing URL list")
                        universeFileList = "local";

                }

            }
            try
            {
                //get file list
                if (!universeFileList.Equals("") && !universeFileList.Equals("local"))
                {

                    using (client = new WebClient())
                    {
                        label_status.Text = "Downloading file list";
                        client.DownloadProgressChanged += client_ProgressChanged;
                        await client.DownloadFileTaskAsync(new Uri(universeFileList), fileName);
                    }
                    var reader = File.OpenText(fileName);
                    var writer = new StreamWriter("SC-URLs.txt", true);

                    seekToLine(reader, "file_list");
                    var line = reader.ReadLine();
                    while (!line.Contains("],"))
                    {
                        fileList.Add(stripQuotations(line));
                        line = reader.ReadLine();
                    }

                    label_status.Text = fileList.Count + " files are ready for download";

                    //find prefix
                    line = seekToLine(reader, "key_prefix");
                    string[] parts = line.Split((char)34);
                    prefix = parts[3];

                    //base url
                    seekToLine(reader, "webseed_urls");
                    line = reader.ReadLine();
                    while (!line.Contains("]"))
                    {
                        baseURLS.Add(stripQuotations(line));
                        line = reader.ReadLine();
                    }

                    foreach (var i in fileList)
                    {
                        Random rand = new Random();
                        var randomBase = rand.Next(0, (baseURLS.Count - 1));

                        urlList.Add(baseURLS[randomBase] + "/" + prefix + "/" + i);
                        await writer.WriteLineAsync(baseURLS[randomBase] + "/" + prefix + "/" + i);
                    }
                    writer.Close();
                    reader.Close();

                    File.Delete(fileName);

                }
                else if (universeFileList.Equals("local"))
                {
                    label_status.Text = "Program will resume from existing file list";
                }
                else
                {
                    MessageBox.Show("Unable to find file list", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                downloadSrt.Visible = true;
            }
            catch (WebException)
            {
                MessageBox.Show("File list failed to download", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private object stripQuotations(string line)
        {
            string[] parts = line.Split('"');
            return parts[1];
        }

        private string seekToLine(StreamReader file, string lineContents)
        {
            string line = "";
            while (!line.Contains(lineContents))
            {
                line = file.ReadLine();
            }
            return (line);


        }

        private void butCancel_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Are you sure you want to cancel?", "Cancel Download", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                client.CancelAsync();
                ResetAllBoxes(this);
                label_status.Text = "Download Cancelled";
                butCancel.Enabled = false;
                releaseSelect.Enabled = true;
                browseDir.Enabled = true;

            }



        }
    }
}