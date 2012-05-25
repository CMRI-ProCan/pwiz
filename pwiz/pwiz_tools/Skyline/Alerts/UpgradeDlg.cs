﻿/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Windows.Forms;

namespace pwiz.Skyline.Alerts
{
    public partial class UpgradeDlg : Form
    {
// ReSharper disable UnusedParameter.Local
        public UpgradeDlg(int licenseVersion)
        {
            InitializeComponent();
        }
// ReSharper restore UnusedParameter.Local

// ReSharper disable MemberCanBeMadeStatic.Local
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://skyline.gs.washington.edu/labkey/wiki/home/software/Skyline/page.view?name=LicenseAgreement");
        }
// ReSharper restore MemberCanBeMadeStatic.Local
    }
}