/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class ImportResultsControl : UserControl
    {
        public ImportResultsControl(SkylineWindow skylineWindow)
        {
            SkylineWindow = skylineWindow;

            InitializeComponent();
        }

        public event EventHandler<ResultsFilesEventArgs> ResultsFilesChanged;

        private void FireResultsFilesChanged(ResultsFilesEventArgs e)
        {
            if (ResultsFilesChanged != null)
            {
                ResultsFilesChanged(this, e);
            }
        }

        private SkylineWindow SkylineWindow { get; set; }
        private Form WizardForm { get { return FormEx.GetParentForm(this); } }


        public List<string> FoundResultsFiles { get; set; }
        public List<string> MissingResultsFiles { get; set; }

        public void InitializeChromatogramsPage(Library docLib)
        {
            FoundResultsFiles = new List<string>();
            MissingResultsFiles = new List<string>();
            if (null != docLib)
            {
                foreach (var dataFile in docLib.LibraryDetails.DataFiles)
                {
                    if (File.Exists(dataFile) && DataSourceUtil.IsDataSource(dataFile))
                    {
                        // We've found the dataFile in the exact location
                        // specified in the document library, so just add it
                        // to the "FOUND" list.
                        FoundResultsFiles.Add(dataFile);
                    }
                    else
                    {
                        MissingResultsFiles.Add(dataFile);
                    }
                }

                docLib.ReadStream.CloseStream();
            }

            UpdateResultsFiles(Path.GetDirectoryName(SkylineWindow.DocumentFilePath));
        }

        public void GetPeptideSearchChromatograms()
        {
            List<KeyValuePair<string, string[]>> namedResults = new List<KeyValuePair<string, string[]>>();
            foreach (string resultsFile in FoundResultsFiles)
            {
                namedResults.Add(new KeyValuePair<string, string[]>(
                                       Path.GetFileNameWithoutExtension(resultsFile), new[] { resultsFile }));
            }

            SkylineWindow.ModifyDocument(Resources.ImportResultsControl_GetPeptideSearchChromatograms_Import_results,
               doc => SkylineWindow.ImportResults(doc, namedResults.ToArray(), ExportOptimize.NONE));
        }

        private void findResultsFilesButton_Click(object sender, EventArgs e)
        {
            // Ask the user for the directory to search
            string initialDir = Path.GetDirectoryName(Path.GetDirectoryName(SkylineWindow.DocumentFilePath));
            FolderBrowserDialog dlg = new FolderBrowserDialog
            {
                Description = Resources.ImportResultsControl_findResultsFilesButton_Click_Results_Directory,
                ShowNewFolderButton = false,
                SelectedPath = initialDir
            };
            if (dlg.ShowDialog(WizardForm) != DialogResult.OK)
                return;

            // See if we're still missing any files, and update UI accordingly
            if (!UpdateResultsFiles(dlg.SelectedPath))
            {
                MessageDlg.Show(WizardForm, Resources.ImportResultsControl_findResultsFilesButton_Click_Could_not_find_all_the_missing_results_files_);
            }
        }

        public bool UpdateResultsFiles(string dirPath)
        {
            // Create a map for the missing results files, where 
            // "missingFiles[key].Found = false" means the file "key" is missing
            Dictionary<string, ResultsFileFindInfo> missingFiles = new Dictionary<string, ResultsFileFindInfo>();
            foreach (var item in MissingResultsFiles)
            {
                missingFiles.Add(item, new ResultsFileFindInfo(string.Empty, false));
            }

            // Add files that were found to the "found results files" list,
            // and remove it from the "missing results files" list.
            bool allFilesFound = FindResultsFiles(dirPath, missingFiles);
            foreach (var item in missingFiles.Keys.Where(item => missingFiles[item].Found))
            {
                FoundResultsFiles.Add(missingFiles[item].Path);
                MissingResultsFiles.Remove(item);
            }

            UpdateResultsFilesUI(allFilesFound);

            FireResultsFilesChanged(new ResultsFilesEventArgs(listResultsFilesMissing.Items.Count));
            return allFilesFound;
        }

        public bool FindResultsFiles(string dirSearch, Dictionary<string, ResultsFileFindInfo> missingFiles)
        {
            int numMissingFiles = missingFiles.Count;
            LongWaitDlg longWaitDlg = new LongWaitDlg
            {
                Text = Resources.ImportResultsControl_FindResultsFiles_Searching_for_Results_Files,
                Message = string.Format(Resources.ImportResultsControl_FindResultsFiles_Searching_for_matching_results_files_in__0__, dirSearch)
            };
            try
            {
                longWaitDlg.PerformWork(WizardForm, 1000, longWaitBroker =>
                       FindDataFiles(longWaitBroker, missingFiles, dirSearch, ref numMissingFiles));
            }
            catch (Exception x)
            {
                MessageDlg.Show(WizardForm, TextUtil.LineSeparate(Resources.ImportResultsControl_FindResultsFiles_An_error_occurred_attempting_to_find_results_files_, x.Message));
            }

            return numMissingFiles == 0;
        }

        private void FindDataFiles(ILongWaitBroker longWaitBroker, Dictionary<string, ResultsFileFindInfo> missingFiles, string directory, ref int numMissingFiles)
        {
            try
            {
                SearchForDataFilesInDirectory(longWaitBroker, missingFiles, directory, ref numMissingFiles);
                if (numMissingFiles == 0)
                {
                    return;
                }

                string[] dirs = Directory.GetDirectories(directory);
                foreach (string dir in dirs)
                {
                    if (longWaitBroker.IsCanceled)
                    {
                        break;
                    }

                    longWaitBroker.Message = String.Format("Searching for missing files in {0}.", dir);
                    FindDataFiles(longWaitBroker, missingFiles, dir, ref numMissingFiles);
                    if (numMissingFiles == 0)
                    {
                        return;
                    }
                }
            }
            catch (Exception x)
            {
                MessageDlg.Show(WizardForm, TextUtil.LineSeparate(string.Format("An error occurred attempting to find the files in {0}.", directory), x.Message));
            }
        }

        private void SearchForDataFilesInDirectory(ILongWaitBroker longWaitBroker, Dictionary<string, ResultsFileFindInfo> missingFiles, string dir, ref int numMissingFiles)
        {
            string[] files = Directory.GetFiles(dir);
            foreach (string file in files)
            {
                if (longWaitBroker.IsCanceled)
                {
                    break;
                }

                if (DataSourceUtil.IsDataSource(file))
                {
                    // Check to see if the file matches any of the missing files
                    string fileName = Path.GetFileName(file);
                    foreach (var item in missingFiles.Keys.Where(item => !missingFiles[item].Found))
                    {
                        if (null != item)
                        {
                            if (Equals(item, fileName) ||
                                MeasuredResults.IsBaseNameMatch(Path.GetFileNameWithoutExtension(item),
                                                                Path.GetFileNameWithoutExtension(file)))
                            {
                                missingFiles[item].Found = true;
                                missingFiles[item].Path = file;
                                numMissingFiles--;
                                if (numMissingFiles == 0)
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void UpdateResultsFilesUI(bool allFilesFound)
        {
            // Update the results files list boxes
            UpdateResultsFilesList(FoundResultsFiles, listResultsFilesFound);
            UpdateResultsFilesList(MissingResultsFiles, listResultsFilesMissing);

            if (allFilesFound)
            {
                resultsSplitContainer.Panel2.Visible = false;
                resultsSplitContainer.Panel1.Dock = DockStyle.Fill;
            }
            else
            {
                resultsSplitContainer.Panel2.Visible = true;
            }            
        }

        private void UpdateResultsFilesList(List<string> resultsFiles, ListBox resultsFilesList)
        {
            string dirInputRoot = PathEx.GetCommonRoot(resultsFiles.ToArray());
            resultsFilesList.Items.Clear();
            foreach (var fileName in resultsFiles)
            {
                resultsFilesList.Items.Add(fileName.Substring(dirInputRoot.Length));
            }
        }

        public class ResultsFileFindInfo
        {
            public string Path { get; set; }
            public bool Found { get; set; }

            public ResultsFileFindInfo(string path, bool found)
            {
                Path = path;
                Found = found;
            }
        }

        public class ResultsFilesEventArgs : EventArgs
        {

            public ResultsFilesEventArgs(int numMissingFiles)
            {
                NumMissingFiles = numMissingFiles;
            }

            public int NumMissingFiles { get; private set; }
        }
    }
}
