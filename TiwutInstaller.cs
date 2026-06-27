using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;

namespace TiwutInstaller
{
    public class InstallerForm : Form
    {
        
        private const int WinWidth = 640;
        private const int WinHeight = 460;
        private const string ConfigUrl = "https://raw.githubusercontent.com/tiwut/Tiwut-Launcher-Windows/refs/heads/main/tiwut-installer-config.json";

        
        private enum Step { Loading, License, Options, Installing, Success, Uninstalling }
        private Step currentStep = Step.Loading;

        
        private string appName = "";
        private string zipUrl = "";
        private string licenseUrl = "";
        private string installDirDefault = "";
        private string exeName = "";
        private bool requireAdmin = false;
        private bool requireRestart = false;
        private string iconUrl = "";
        private bool optDesktop = true;
        private bool optStartMenu = true;
        private bool optTaskbar = true;

        
        private string licenseText = "";
        private string actualInstallDir = "";

        
        private Color bgColor;
        private Color cardColor;
        private Color textPrimary;
        private Color textSecondary;
        private Color accentColor;
        private Color accentHover;
        private Color accentActive;
        private Color borderColor;
        private Color controlBg;
        private Color controlBorder;

        
        private Panel titleBar;
        private Panel contentPanel;
        private Panel bottomBar;
        private Label titleLabel;
        private FlatButton btnCancel;
        private FlatButton btnNext;
        private FlatButton btnBack;

        
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        
        private TextBox txtPath;
        private FlatCheckbox chkDesktop;
        private FlatCheckbox chkStartMenu;
        private FlatCheckbox chkTaskbar;

        
        private RichTextBox rtfLicense;
        private FlatCheckbox chkAgree;

        
        private CustomProgressBar progressBar;
        private ListBox lstLog;
        private Label lblProgressStatus;

        
        private FlatCheckbox chkLaunchApp;

        
        private Thread workThread;
        private string tempZipPath = "";
        private string tempIconPath = "";

        
        private bool isUninstallMode = false;

        [STAThread]
        public static void Main(string[] args)
        {
            
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072 | (System.Net.SecurityProtocolType)768 | (System.Net.SecurityProtocolType)192;
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool uninstall = false;
            foreach (var arg in args)
            {
                if (arg.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) || arg.Equals("-uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    uninstall = true;
                }
            }

            InstallerForm form = new InstallerForm(uninstall);
            Application.Run(form);
        }

        public InstallerForm(bool uninstall)
        {
            this.isUninstallMode = uninstall;
            
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            
            this.Size = new Size(WinWidth, WinHeight);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;

            
            ApplyTheme();

            
            InitializeLayout();

            
            if (isUninstallMode)
            {
                SwitchToStep(Step.Uninstalling);
            }
            else
            {
                SwitchToStep(Step.Loading);
                StartConfigLoad();
            }
        }

        private void ApplyTheme()
        {
            bool isLightTheme = IsWindowsLightTheme();

            
            if (isLightTheme)
            {
                bgColor = Color.FromArgb(243, 244, 246);       
                cardColor = Color.White;                       
                textPrimary = Color.FromArgb(17, 24, 39);      
                textSecondary = Color.FromArgb(75, 85, 99);    
                accentColor = Color.FromArgb(79, 70, 229);     
                accentHover = Color.FromArgb(99, 102, 241);    
                accentActive = Color.FromArgb(67, 56, 202);    
                borderColor = Color.FromArgb(229, 231, 235);   
                controlBg = Color.FromArgb(249, 250, 251);     
                controlBorder = Color.FromArgb(209, 213, 219); 
            }
            else
            {
                bgColor = Color.FromArgb(9, 9, 11);            
                cardColor = Color.FromArgb(24, 24, 27);         
                textPrimary = Color.FromArgb(244, 244, 245);   
                textSecondary = Color.FromArgb(161, 161, 170); 
                accentColor = Color.FromArgb(99, 102, 241);    
                accentHover = Color.FromArgb(129, 140, 248);   
                accentActive = Color.FromArgb(79, 70, 229);    
                borderColor = Color.FromArgb(39, 39, 42);      
                controlBg = Color.FromArgb(39, 39, 42);        
                controlBorder = Color.FromArgb(63, 63, 70);    
            }

            this.BackColor = bgColor;
        }

        private bool IsWindowsLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val != null)
                        {
                            return (int)val != 0;
                        }
                    }
                }
            }
            catch { }
            return false; 
        }

        private void InitializeLayout()
        {
            
            titleBar = new Panel();
            titleBar.Size = new Size(WinWidth, 42);
            titleBar.Dock = DockStyle.Top;
            titleBar.BackColor = cardColor;
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseUp += TitleBar_MouseUp;

            titleLabel = new Label();
            titleLabel.Text = isUninstallMode ? "Uninstall Wizard" : "Setup Wizard";
            titleLabel.Font = new Font("Segoe UI Semibold", 10.5f);
            titleLabel.ForeColor = textPrimary;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(14, 11);
            titleBar.Controls.Add(titleLabel);

            
            FlatButton btnClose = new FlatButton();
            btnClose.Text = "✕";
            btnClose.Font = new Font("Segoe UI", 9f);
            btnClose.Size = new Size(40, 30);
            btnClose.Location = new Point(WinWidth - 44, 6);
            btnClose.NormalColor = Color.Transparent;
            btnClose.HoverColor = Color.FromArgb(239, 68, 68); 
            btnClose.ActiveColor = Color.FromArgb(220, 38, 38);
            btnClose.TextColor = textSecondary;
            btnClose.Click += (s, e) => Application.Exit();
            titleBar.Controls.Add(btnClose);

            
            bottomBar = new Panel();
            bottomBar.Size = new Size(WinWidth, 62);
            bottomBar.Dock = DockStyle.Bottom;
            bottomBar.BackColor = cardColor;

            btnCancel = new FlatButton();
            btnCancel.Text = "Cancel";
            btnCancel.Font = new Font("Segoe UI", 9.5f);
            btnCancel.Size = new Size(95, 34);
            btnCancel.Location = new Point(WinWidth - 110, 14);
            btnCancel.NormalColor = controlBg;
            btnCancel.HoverColor = controlBorder;
            btnCancel.ActiveColor = borderColor;
            btnCancel.TextColor = textPrimary;
            btnCancel.Click += btnCancel_Click;

            btnNext = new FlatButton();
            btnNext.Text = "Next";
            btnNext.Font = new Font("Segoe UI Semibold", 9.5f);
            btnNext.Size = new Size(105, 34);
            btnNext.Location = new Point(WinWidth - 225, 14);
            btnNext.NormalColor = accentColor;
            btnNext.HoverColor = accentHover;
            btnNext.ActiveColor = accentActive;
            btnNext.TextColor = Color.White;
            btnNext.Click += btnNext_Click;

            btnBack = new FlatButton();
            btnBack.Text = "Back";
            btnBack.Font = new Font("Segoe UI", 9.5f);
            btnBack.Size = new Size(95, 34);
            btnBack.Location = new Point(WinWidth - 330, 14);
            btnBack.NormalColor = controlBg;
            btnBack.HoverColor = controlBorder;
            btnBack.ActiveColor = borderColor;
            btnBack.TextColor = textPrimary;
            btnBack.Click += btnBack_Click;

            bottomBar.Controls.Add(btnCancel);
            bottomBar.Controls.Add(btnNext);
            bottomBar.Controls.Add(btnBack);

            
            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Padding = new Padding(24);

            this.Controls.Add(contentPanel);
            this.Controls.Add(titleBar);
            this.Controls.Add(bottomBar);
        }

        #region Custom Border Drawing
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            using (Pen borderPen = new Pen(borderColor, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }
        #endregion

        #region TitleBar Dragging Methods
        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }
        #endregion

        private void SwitchToStep(Step step)
        {
            currentStep = step;
            contentPanel.Controls.Clear();

            
            ApplyTheme();

            switch (step)
            {
                case Step.Loading:
                    RenderLoadingStep();
                    break;
                case Step.License:
                    RenderLicenseStep();
                    break;
                case Step.Options:
                    RenderOptionsStep();
                    break;
                case Step.Installing:
                    RenderInstallingStep();
                    break;
                case Step.Success:
                    RenderSuccessStep();
                    break;
                case Step.Uninstalling:
                    RenderUninstallingStep();
                    break;
            }

            contentPanel.Invalidate();
            bottomBar.Invalidate();
        }

        #region Step Renderers

        private void RenderLoadingStep()
        {
            titleLabel.Text = "Setup - Preparing installation";
            btnBack.Visible = false;
            btnNext.Visible = false;
            btnCancel.Enabled = true;

            Label lblWelcome = new Label();
            lblWelcome.Text = "Welcome to the Installer";
            lblWelcome.Font = new Font("Segoe UI Semibold", 18f);
            lblWelcome.ForeColor = textPrimary;
            lblWelcome.AutoSize = true;
            lblWelcome.Location = new Point(24, 60);

            Label lblStatus = new Label();
            lblStatus.Text = "Downloading installation profile...";
            lblStatus.Font = new Font("Segoe UI", 10f);
            lblStatus.ForeColor = textSecondary;
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(24, 110);
            lblStatus.Name = "lblLoadingStatus";

            CustomProgressBar loaderBar = new CustomProgressBar();
            loaderBar.Size = new Size(WinWidth - 80, 8);
            loaderBar.Location = new Point(24, 150);
            loaderBar.Value = 25;
            loaderBar.ProgressColor1 = accentColor;
            loaderBar.ProgressColor2 = accentHover;
            loaderBar.ProgressBgColor = controlBg;
            loaderBar.BorderColor = borderColor;
            loaderBar.Name = "loaderBar";

            contentPanel.Controls.Add(lblWelcome);
            contentPanel.Controls.Add(lblStatus);
            contentPanel.Controls.Add(loaderBar);
        }

        private void RenderLicenseStep()
        {
            titleLabel.Text = "Setup - License Agreement";
            btnBack.Visible = true;
            btnBack.Enabled = true;
            btnNext.Visible = true;
            btnNext.Text = "Next";
            btnNext.Enabled = false; 
            btnCancel.Enabled = true;

            Label lblTitle = new Label();
            lblTitle.Text = "License Agreement";
            lblTitle.Font = new Font("Segoe UI Semibold", 15f);
            lblTitle.ForeColor = textPrimary;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(24, 10);

            Label lblDesc = new Label();
            lblDesc.Text = "Please review the license terms before installing " + appName + ".";
            lblDesc.Font = new Font("Segoe UI", 9.5f);
            lblDesc.ForeColor = textSecondary;
            lblDesc.AutoSize = true;
            lblDesc.Location = new Point(24, 45);

            rtfLicense = new RichTextBox();
            rtfLicense.Size = new Size(WinWidth - 80, 180);
            rtfLicense.Location = new Point(24, 75);
            rtfLicense.ReadOnly = true;
            rtfLicense.BackColor = cardColor;
            rtfLicense.ForeColor = textPrimary;
            rtfLicense.BorderStyle = BorderStyle.FixedSingle;
            rtfLicense.Text = licenseText;
            rtfLicense.Font = new Font("Consolas", 8.5f);

            chkAgree = new FlatCheckbox();
            chkAgree.Text = "I accept the terms in the License Agreement";
            chkAgree.Font = new Font("Segoe UI", 9.5f);
            chkAgree.Size = new Size(400, 24);
            chkAgree.Location = new Point(24, 265);
            chkAgree.TextColor = textPrimary;
            chkAgree.AccentColor = accentColor;
            chkAgree.BoxBgColor = cardColor;
            chkAgree.BoxBorderColor = controlBorder;
            chkAgree.Checked = false;
            chkAgree.CheckedChanged += (s, e) => {
                btnNext.Enabled = chkAgree.Checked;
            };

            contentPanel.Controls.Add(lblTitle);
            contentPanel.Controls.Add(lblDesc);
            contentPanel.Controls.Add(rtfLicense);
            contentPanel.Controls.Add(chkAgree);
        }

        private void RenderOptionsStep()
        {
            titleLabel.Text = "Setup - Installation Options";
            btnBack.Visible = true;
            btnBack.Enabled = true;
            btnNext.Visible = true;
            btnNext.Text = "Install";
            btnNext.Enabled = true;
            btnCancel.Enabled = true;

            Label lblTitle = new Label();
            lblTitle.Text = "Choose Destination Folder";
            lblTitle.Font = new Font("Segoe UI Semibold", 15f);
            lblTitle.ForeColor = textPrimary;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(24, 10);

            Label lblPathDesc = new Label();
            lblPathDesc.Text = "The installer will install the application to the folder below.";
            lblPathDesc.Font = new Font("Segoe UI", 9.5f);
            lblPathDesc.ForeColor = textSecondary;
            lblPathDesc.AutoSize = true;
            lblPathDesc.Location = new Point(24, 42);

            txtPath = new TextBox();
            txtPath.Size = new Size(WinWidth - 210, 26);
            txtPath.Location = new Point(24, 75);
            txtPath.Font = new Font("Segoe UI", 9.5f);
            txtPath.BackColor = cardColor;
            txtPath.ForeColor = textPrimary;
            txtPath.BorderStyle = BorderStyle.FixedSingle;
            txtPath.Text = actualInstallDir;

            FlatButton btnBrowse = new FlatButton();
            btnBrowse.Text = "Browse...";
            btnBrowse.Font = new Font("Segoe UI", 9f);
            btnBrowse.Size = new Size(90, 26);
            btnBrowse.Location = new Point(WinWidth - 176, 75);
            btnBrowse.NormalColor = controlBg;
            btnBrowse.HoverColor = controlBorder;
            btnBrowse.ActiveColor = borderColor;
            btnBrowse.TextColor = textPrimary;
            btnBrowse.Click += btnBrowse_Click;

            Label lblOptionsTitle = new Label();
            lblOptionsTitle.Text = "Create Shortcuts";
            lblOptionsTitle.Font = new Font("Segoe UI Semibold", 12f);
            lblOptionsTitle.ForeColor = textPrimary;
            lblOptionsTitle.AutoSize = true;
            lblOptionsTitle.Location = new Point(24, 125);

            chkDesktop = new FlatCheckbox();
            chkDesktop.Text = "Create Desktop Shortcut";
            chkDesktop.Font = new Font("Segoe UI", 9.5f);
            chkDesktop.Size = new Size(300, 24);
            chkDesktop.Location = new Point(24, 160);
            chkDesktop.TextColor = textPrimary;
            chkDesktop.AccentColor = accentColor;
            chkDesktop.BoxBgColor = cardColor;
            chkDesktop.BoxBorderColor = controlBorder;
            chkDesktop.Checked = optDesktop;

            chkStartMenu = new FlatCheckbox();
            chkStartMenu.Text = "Create Start Menu Folder & Shortcut";
            chkStartMenu.Font = new Font("Segoe UI", 9.5f);
            chkStartMenu.Size = new Size(300, 24);
            chkStartMenu.Location = new Point(24, 195);
            chkStartMenu.TextColor = textPrimary;
            chkStartMenu.AccentColor = accentColor;
            chkStartMenu.BoxBgColor = cardColor;
            chkStartMenu.BoxBorderColor = controlBorder;
            chkStartMenu.Checked = optStartMenu;

            chkTaskbar = new FlatCheckbox();
            chkTaskbar.Text = "Pin to Taskbar / Quick Launch (Best Effort)";
            chkTaskbar.Font = new Font("Segoe UI", 9.5f);
            chkTaskbar.Size = new Size(300, 24);
            chkTaskbar.Location = new Point(24, 230);
            chkTaskbar.TextColor = textPrimary;
            chkTaskbar.AccentColor = accentColor;
            chkTaskbar.BoxBgColor = cardColor;
            chkTaskbar.BoxBorderColor = controlBorder;
            chkTaskbar.Checked = optTaskbar;

            contentPanel.Controls.Add(lblTitle);
            contentPanel.Controls.Add(lblPathDesc);
            contentPanel.Controls.Add(txtPath);
            contentPanel.Controls.Add(btnBrowse);
            contentPanel.Controls.Add(lblOptionsTitle);
            contentPanel.Controls.Add(chkDesktop);
            contentPanel.Controls.Add(chkStartMenu);
            contentPanel.Controls.Add(chkTaskbar);
        }

        private void RenderInstallingStep()
        {
            titleLabel.Text = isUninstallMode ? "Uninstalling " + appName : "Installing " + appName;
            btnBack.Visible = false;
            btnNext.Visible = false;
            btnCancel.Enabled = false; 

            Label lblTitle = new Label();
            lblTitle.Text = isUninstallMode ? "Removing application files..." : "Installing program...";
            lblTitle.Font = new Font("Segoe UI Semibold", 15f);
            lblTitle.ForeColor = textPrimary;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(24, 10);

            lblProgressStatus = new Label();
            lblProgressStatus.Text = isUninstallMode ? "Initializing uninstaller..." : "Starting download...";
            lblProgressStatus.Font = new Font("Segoe UI", 9.5f);
            lblProgressStatus.ForeColor = textSecondary;
            lblProgressStatus.AutoSize = true;
            lblProgressStatus.Location = new Point(24, 40);

            progressBar = new CustomProgressBar();
            progressBar.Size = new Size(WinWidth - 80, 12);
            progressBar.Location = new Point(24, 65);
            progressBar.Value = 0;
            progressBar.ProgressColor1 = accentColor;
            progressBar.ProgressColor2 = accentHover;
            progressBar.ProgressBgColor = controlBg;
            progressBar.BorderColor = borderColor;

            lstLog = new ListBox();
            lstLog.Size = new Size(WinWidth - 80, 190);
            lstLog.Location = new Point(24, 95);
            lstLog.BackColor = cardColor;
            lstLog.ForeColor = textSecondary;
            lstLog.BorderStyle = BorderStyle.FixedSingle;
            lstLog.Font = new Font("Consolas", 8.5f);

            contentPanel.Controls.Add(lblTitle);
            contentPanel.Controls.Add(lblProgressStatus);
            contentPanel.Controls.Add(progressBar);
            contentPanel.Controls.Add(lstLog);
        }

        private void RenderSuccessStep()
        {
            titleLabel.Text = isUninstallMode ? "Uninstall Completed" : "Setup Complete";
            btnBack.Visible = false;
            btnNext.Visible = true;
            btnNext.Text = "Finish";
            btnNext.Enabled = true;
            btnCancel.Visible = false;

            Label lblTitle = new Label();
            lblTitle.Text = isUninstallMode ? "Uninstall Completed Successfully!" : "Installation Completed!";
            lblTitle.Font = new Font("Segoe UI Semibold", 18f);
            lblTitle.ForeColor = textPrimary;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(24, 40);

            Label lblDesc = new Label();
            lblDesc.Text = isUninstallMode 
                ? appName + " has been successfully removed from your computer."
                : appName + " has been successfully installed on your computer. You can now launch it.";
            lblDesc.Font = new Font("Segoe UI", 10f);
            lblDesc.ForeColor = textSecondary;
            lblDesc.Size = new Size(WinWidth - 80, 50);
            lblDesc.Location = new Point(24, 90);

            if (!isUninstallMode)
            {
                chkLaunchApp = new FlatCheckbox();
                chkLaunchApp.Text = "Launch " + appName + " now";
                chkLaunchApp.Font = new Font("Segoe UI Semibold", 10f);
                chkLaunchApp.Size = new Size(300, 24);
                chkLaunchApp.Location = new Point(24, 150);
                chkLaunchApp.TextColor = textPrimary;
                chkLaunchApp.AccentColor = accentColor;
                chkLaunchApp.BoxBgColor = cardColor;
                chkLaunchApp.BoxBorderColor = controlBorder;
                chkLaunchApp.Checked = true;
                contentPanel.Controls.Add(chkLaunchApp);
            }

            if (requireRestart)
            {
                Label lblRestart = new Label();
                lblRestart.Text = "★ A computer restart is recommended to complete the configuration.";
                lblRestart.Font = new Font("Segoe UI", 9.5f);
                lblRestart.ForeColor = accentColor;
                lblRestart.AutoSize = true;
                lblRestart.Location = new Point(24, 190);
                contentPanel.Controls.Add(lblRestart);
            }

            contentPanel.Controls.Add(lblTitle);
            contentPanel.Controls.Add(lblDesc);
        }

        private void RenderUninstallingStep()
        {
            RenderInstallingStep();
            
            workThread = new Thread(RunUninstallProcess);
            workThread.IsBackground = true;
            workThread.Start();
        }

        #endregion

        #region Asynchronous Installer Operations

        private void StartConfigLoad()
        {
            Thread t = new Thread(() => {
                
                string json = "";
                int maxRetries = 3;
                bool downloadSuccess = false;
                Exception lastError = null;

                for (int i = 1; i <= maxRetries; i++)
                {
                    try
                    {
                        UpdateLoadProgress("Downloading configuration profile (Attempt " + i + ")...", 20 + i * 15);
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0");
                            json = wc.DownloadString(ConfigUrl);
                            downloadSuccess = true;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        if (i < maxRetries)
                        {
                            Thread.Sleep(1500); 
                        }
                    }
                }

                if (!downloadSuccess)
                {
                    Invoke((Action)(() => {
                        DialogResult result = MessageBox.Show(
                            "Failed to download the installation configuration from the network after " + maxRetries + " attempts.\n\n" +
                            "Error details: " + (lastError != null ? lastError.Message : "Unknown Network Error") + "\n\n" +
                            "Would you like to retry?",
                            "Connection Error",
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Error
                        );
                        if (result == DialogResult.Retry)
                        {
                            SwitchToStep(Step.Loading);
                            StartConfigLoad();
                        }
                        else
                        {
                            Application.Exit();
                        }
                    }));
                    return;
                }

                UpdateLoadProgress("Applying configuration settings...", 70);

                
                if (!string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(json.Trim()))
                {
                    try
                    {
                        appName = GetJsonString(json, "appName", "");
                        zipUrl = GetJsonString(json, "zipUrl", "");
                        licenseUrl = GetJsonString(json, "licenseUrl", "");
                        installDirDefault = GetJsonString(json, "installDir", "");
                        exeName = GetJsonString(json, "exeName", "");
                        requireAdmin = GetJsonBool(json, "requireAdmin", false);
                        requireRestart = GetJsonBool(json, "requireRestart", false);
                        iconUrl = GetJsonString(json, "iconUrl", "");
                        
                        
                        optDesktop = GetJsonBool(json, "desktop", true);
                        optStartMenu = GetJsonBool(json, "startMenu", true);
                        optTaskbar = GetJsonBool(json, "taskbar", true);
                    }
                    catch (Exception ex)
                    {
                        Invoke((Action)(() => LogMessage("Warning parsing configuration JSON: " + ex.Message)));
                    }
                }

                
                if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(zipUrl) || string.IsNullOrEmpty(installDirDefault) || string.IsNullOrEmpty(exeName))
                {
                    Invoke((Action)(() => {
                        MessageBox.Show(
                            "The installation configuration fetched from the remote server is empty, invalid, or missing required fields.\n\n" +
                            "Make sure that your remote config JSON defines:\n" +
                            " - appName\n - zipUrl\n - installDir\n - exeName",
                            "Invalid Installation Profile",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        Application.Exit();
                    }));
                    return;
                }

                
                actualInstallDir = Environment.ExpandEnvironmentVariables(installDirDefault);

                
                if (requireAdmin && !IsRunAsAdmin())
                {
                    Invoke((Action)(() => ElevateProcess()));
                    return;
                }

                
                if (!string.IsNullOrEmpty(licenseUrl))
                {
                    UpdateLoadProgress("Downloading License Agreement...", 85);
                    try
                    {
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0");
                            licenseText = wc.DownloadString(licenseUrl);
                        }
                    }
                    catch
                    {
                        licenseText = "Standard Open Source License Agreement.\n\nBy clicking 'Next', you agree to install and use this application under its license terms.\n\nIf you do not accept these terms, click 'Cancel' to exit the installation.";
                    }
                }
                else
                {
                    licenseText = "Standard Installation License Agreement.\n\nClick 'Next' to proceed with the setup.";
                }

                UpdateLoadProgress("Ready", 100);

                
                Invoke((Action)(() => {
                    if (!string.IsNullOrEmpty(licenseUrl))
                        SwitchToStep(Step.License);
                    else
                        SwitchToStep(Step.Options);
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        private void UpdateLoadProgress(string text, int percent)
        {
            Invoke((Action)(() => {
                var lbl = contentPanel.Controls["lblLoadingStatus"] as Label;
                if (lbl != null) lbl.Text = text;
                var bar = contentPanel.Controls["loaderBar"] as CustomProgressBar;
                if (bar != null) bar.Value = percent;
            }));
        }

        private void ElevateProcess()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.Verb = "runas";
            psi.FileName = Application.ExecutablePath;
            psi.UseShellExecute = true;
            try
            {
                Process.Start(psi);
                Application.Exit();
            }
            catch
            {
                MessageBox.Show("Administrative privileges are required to run this installation.", "Elevation Refused", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Application.Exit();
            }
        }

        private bool IsRunAsAdmin()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void StartInstallation()
        {
            SwitchToStep(Step.Installing);

            workThread = new Thread(RunInstallProcess);
            workThread.IsBackground = true;
            workThread.Start();
        }

        private void RunInstallProcess()
        {
            try
            {
                LogMessage("Initializing installation process...");
                Thread.Sleep(500);

                
                string processName = Path.GetFileNameWithoutExtension(exeName);
                LogMessage("Checking for running instances of " + processName + "...");
                try
                {
                    foreach (var proc in Process.GetProcessesByName(processName))
                    {
                        LogMessage("Found active instance (PID " + proc.Id + "). Terminating process to prevent file locking...");
                        proc.Kill();
                        proc.WaitForExit(3000);
                        LogMessage("Process terminated successfully.");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage("Warning checking running processes: " + ex.Message);
                }

                
                LogMessage("Checking target directory: " + actualInstallDir);
                if (!Directory.Exists(actualInstallDir))
                {
                    Directory.CreateDirectory(actualInstallDir);
                    LogMessage("Target directory created.");
                }

                
                try
                {
                    string testPath = Path.Combine(actualInstallDir, "write_test.tmp");
                    File.WriteAllText(testPath, "test");
                    File.Delete(testPath);
                    LogMessage("Write permission verification passed.");
                }
                catch (Exception ex)
                {
                    throw new Exception("Write verification failed for target folder: " + ex.Message);
                }

                
                LogMessage("Downloading package from: " + zipUrl);
                tempZipPath = Path.Combine(Path.GetTempPath(), appName + "_install.zip");
                
                int maxZipRetries = 3;
                bool zipDownloadSuccess = false;
                Exception lastZipError = null;

                for (int attempt = 1; attempt <= maxZipRetries; attempt++)
                {
                    try
                    {
                        LogMessage("Downloading application package (Attempt " + attempt + " of " + maxZipRetries + ")...");
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0");
                            wc.DownloadProgressChanged += (s, e) => {
                                UpdateProgress("Downloading package: " + e.ProgressPercentage + "%", (e.ProgressPercentage / 2));
                            };
                            
                            AutoResetEvent are = new AutoResetEvent(false);
                            Exception downloadError = null;
                            
                            wc.DownloadFileCompleted += (s, e) => {
                                if (e.Error != null)
                                    downloadError = e.Error;
                                are.Set();
                            };
                            
                            wc.DownloadFileAsync(new Uri(zipUrl), tempZipPath);
                            are.WaitOne();

                            if (downloadError != null)
                                throw downloadError;

                            
                            if (!File.Exists(tempZipPath) || new FileInfo(tempZipPath).Length == 0)
                            {
                                throw new Exception("Downloaded zip package is missing or empty.");
                            }

                            
                            using (FileStream fs = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read))
                            {
                                if (fs.Length < 4)
                                    throw new Exception("Downloaded file is too small to be a valid ZIP archive.");
                                byte[] header = new byte[4];
                                fs.Read(header, 0, 4);
                                if (header[0] != 0x50 || header[1] != 0x4B || header[2] != 0x03 || header[3] != 0x04)
                                {
                                    throw new Exception("Downloaded file has invalid magic headers. Not a valid ZIP file.");
                                }
                            }

                            zipDownloadSuccess = true;
                            break; 
                        }
                    }
                    catch (Exception ex)
                    {
                        lastZipError = ex;
                        LogMessage("Download attempt " + attempt + " failed: " + ex.Message);
                        if (File.Exists(tempZipPath))
                        {
                            try { File.Delete(tempZipPath); } catch { }
                        }
                        if (attempt < maxZipRetries)
                        {
                            Thread.Sleep(2000); 
                        }
                    }
                }

                if (!zipDownloadSuccess)
                {
                    throw new Exception("Failed to download package after " + maxZipRetries + " attempts. Error details: " + (lastZipError != null ? lastZipError.Message : "Unknown"));
                }
                LogMessage("Application package downloaded and verified successfully.");

                
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    try
                    {
                        LogMessage("Downloading application icon...");
                        tempIconPath = Path.Combine(actualInstallDir, "app.ico");
                        using (WebClient wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0");
                            wc.DownloadFile(new Uri(iconUrl), tempIconPath);
                            LogMessage("Application icon saved to destination.");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage("Could not retrieve custom icon (" + ex.Message + "). Using default binary icon.");
                        tempIconPath = "";
                    }
                }

                
                LogMessage("Extracting package to: " + actualInstallDir);
                UpdateProgress("Extracting application files...", 60);
                
                
                if (Directory.Exists(actualInstallDir))
                {
                    foreach (var file in Directory.GetFiles(actualInstallDir))
                    {
                        if (Path.GetFileName(file).Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (Path.GetFileName(file).Equals("app.ico", StringComparison.OrdinalIgnoreCase)) continue;
                        try { File.Delete(file); } catch { }
                    }
                }

                using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                {
                    int totalEntries = archive.Entries.Count;
                    int extracted = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(actualInstallDir, entry.FullName));
                        
                        
                        if (!destinationPath.StartsWith(actualInstallDir, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception("Security alert: Attempted Zip Slip extraction traversal prevented.");
                        }

                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else if (entry.Length == 0 && string.IsNullOrEmpty(Path.GetFileName(entry.FullName)))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                            
                            
                            int fileWriteAttempts = 3;
                            for (int wAttempt = 1; wAttempt <= fileWriteAttempts; wAttempt++)
                            {
                                try
                                {
                                    entry.ExtractToFile(destinationPath, true);
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    if (wAttempt == fileWriteAttempts)
                                    {
                                        throw new Exception("Failed to write extracted file '" + Path.GetFileName(destinationPath) + "'. File might be locked. Error: " + ex.Message);
                                    }
                                    Thread.Sleep(1000); 
                                }
                            }

                            extracted++;
                            int extractPercent = 60 + (int)((extracted / (float)totalEntries) * 20);
                            UpdateProgress("Extracting: " + entry.Name, extractPercent);
                        }
                    }
                }
                LogMessage("Extraction complete. Extracted files.");

                
                try { File.Delete(tempZipPath); } catch { }

                
                string mainExePath = Path.Combine(actualInstallDir, exeName);
                if (!File.Exists(mainExePath))
                {
                    LogMessage("Warning: Config executable not found at specified path. Scanning extracted directories...");
                    string[] foundExes = Directory.GetFiles(actualInstallDir, "*.exe", SearchOption.AllDirectories);
                    if (foundExes.Length > 0)
                    {
                        foreach (var f in foundExes)
                        {
                            if (!Path.GetFileName(f).Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                mainExePath = f;
                                exeName = mainExePath.Substring(actualInstallDir.Length).TrimStart('\\');
                                LogMessage("Found executable at: " + exeName);
                                break;
                            }
                        }
                    }
                }

                
                string shortcutDesc = "Launcher for " + appName;
                string iconLocation = string.IsNullOrEmpty(tempIconPath) ? mainExePath : tempIconPath;

                if (optDesktop)
                {
                    LogMessage("Creating Desktop shortcut...");
                    string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), appName + ".lnk");
                    CreateShortcut(desktopPath, mainExePath, actualInstallDir, shortcutDesc, iconLocation);
                }

                if (optStartMenu)
                {
                    LogMessage("Creating Start Menu shortcuts...");
                    string startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), appName);
                    if (!Directory.Exists(startMenuFolder))
                    {
                        Directory.CreateDirectory(startMenuFolder);
                    }
                    string startMenuLnk = Path.Combine(startMenuFolder, appName + ".lnk");
                    CreateShortcut(startMenuLnk, mainExePath, actualInstallDir, shortcutDesc, iconLocation);
                }

                if (optTaskbar)
                {
                    LogMessage("Creating Taskbar shortcut (Best Effort)...");
                    string taskbarDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
                    if (Directory.Exists(taskbarDir))
                    {
                        string taskbarLnk = Path.Combine(taskbarDir, appName + ".lnk");
                        CreateShortcut(taskbarLnk, mainExePath, actualInstallDir, shortcutDesc, iconLocation);
                        LogMessage("Taskbar shortcut written to pinned folder.");
                    }
                    else
                    {
                        LogMessage("Taskbar pinned folder not found. Skipping.");
                    }
                }

                UpdateProgress("Configuring registry values...", 90);

                
                LogMessage("Registering uninstaller in Add/Remove programs...");
                
                string uninstallerPath = Path.Combine(actualInstallDir, "Uninstall.exe");
                try
                {
                    File.Copy(Application.ExecutablePath, uninstallerPath, true);
                }
                catch (Exception ex)
                {
                    LogMessage("Warning copying uninstaller: " + ex.Message);
                }

                RegisterUninstallEntry(uninstallerPath, mainExePath);
                LogMessage("System entries registered successfully.");

                UpdateProgress("Finishing installation...", 100);
                Thread.Sleep(600);

                Invoke((Action)(() => SwitchToStep(Step.Success)));
            }
            catch (Exception ex)
            {
                LogMessage("ERROR: Installation failed!");
                LogMessage(ex.Message);
                Invoke((Action)(() => {
                    MessageBox.Show("An error occurred during installation:\n" + ex.Message, "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnCancel.Enabled = true;
                    btnCancel.Visible = true;
                    btnCancel.Text = "Close";
                }));
            }
            finally
            {
                
                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); } catch { }
                }
            }
        }

        private void RegisterUninstallEntry(string uninstallerPath, string mainExePath)
        {
            try
            {
                RegistryKey baseKey = IsRunAsAdmin() ? Registry.LocalMachine : Registry.CurrentUser;
                string regPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + appName;

                using (RegistryKey key = baseKey.CreateSubKey(regPath))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", appName);
                        key.SetValue("UninstallString", "\"" + uninstallerPath + "\" /uninstall");
                        key.SetValue("InstallLocation", actualInstallDir);
                        key.SetValue("DisplayIcon", string.IsNullOrEmpty(tempIconPath) ? mainExePath : tempIconPath);
                        key.SetValue("Publisher", "Tiwut Software");
                        key.SetValue("DisplayVersion", "1.0.0");
                        key.SetValue("NoModify", 1);
                        key.SetValue("NoRepair", 1);
                        
                        
                        long totalSize = 0;
                        if (Directory.Exists(actualInstallDir))
                        {
                            foreach (var file in Directory.GetFiles(actualInstallDir, "*", SearchOption.AllDirectories))
                            {
                                try { totalSize += new FileInfo(file).Length; } catch { }
                            }
                        }
                        key.SetValue("EstimatedSize", (int)(totalSize / 1024), RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage("Warning: Registry uninstaller registration failed: " + ex.Message);
            }
        }

        private void RunUninstallProcess()
        {
            try
            {
                LogMessage("Initializing uninstallation process...");
                Thread.Sleep(500);

                
                RegistryKey baseKey = IsRunAsAdmin() ? Registry.LocalMachine : Registry.CurrentUser;
                string regPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + appName;

                if (string.IsNullOrEmpty(actualInstallDir))
                {
                    using (RegistryKey key = baseKey.OpenSubKey(regPath))
                    {
                        if (key != null)
                        {
                            actualInstallDir = key.GetValue("InstallLocation") as string;
                        }
                    }
                }

                if (string.IsNullOrEmpty(actualInstallDir))
                {
                    actualInstallDir = Environment.ExpandEnvironmentVariables(installDirDefault);
                }

                LogMessage("Uninstall location identified: " + actualInstallDir);

                
                LogMessage("Terminating active running instances of application processes...");
                try
                {
                    string processName = Path.GetFileNameWithoutExtension(exeName);
                    foreach (var proc in Process.GetProcessesByName(processName))
                    {
                        LogMessage("Killing process: " + proc.Id);
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                }
                catch { }

                
                LogMessage("Deleting application shortcuts...");
                UpdateProgress("Removing shortcuts...", 30);
                try
                {
                    string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), appName + ".lnk");
                    if (File.Exists(desktopPath)) File.Delete(desktopPath);

                    string startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), appName);
                    if (Directory.Exists(startMenuFolder))
                    {
                        Directory.Delete(startMenuFolder, true);
                    }

                    string taskbarDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
                    string taskbarLnk = Path.Combine(taskbarDir, appName + ".lnk");
                    if (File.Exists(taskbarLnk)) File.Delete(taskbarLnk);
                }
                catch (Exception ex)
                {
                    LogMessage("Warning deleting shortcuts: " + ex.Message);
                }

                
                LogMessage("Deleting registry settings...");
                UpdateProgress("Updating registry values...", 60);
                try
                {
                    baseKey.DeleteSubKeyTree(regPath, false);
                }
                catch (Exception ex)
                {
                    LogMessage("Warning deleting registry tree: " + ex.Message);
                }

                
                LogMessage("Cleaning up local application directory...");
                UpdateProgress("Purging files...", 80);
                if (Directory.Exists(actualInstallDir))
                {
                    foreach (var file in Directory.GetFiles(actualInstallDir))
                    {
                        
                        if (Path.GetFileName(file).Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase))
                            continue;
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            LogMessage("Could not delete file " + Path.GetFileName(file) + ": " + ex.Message);
                        }
                    }

                    foreach (var dir in Directory.GetDirectories(actualInstallDir))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch (Exception ex)
                        {
                            LogMessage("Could not delete folder " + Path.GetFileName(dir) + ": " + ex.Message);
                        }
                    }
                }

                UpdateProgress("Uninstall complete.", 100);
                Thread.Sleep(600);

                Invoke((Action)(() => SwitchToStep(Step.Success)));
            }
            catch (Exception ex)
            {
                LogMessage("ERROR: Uninstallation failed!");
                LogMessage(ex.Message);
                Invoke((Action)(() => {
                    MessageBox.Show("An error occurred during uninstallation:\n" + ex.Message, "Uninstall Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnCancel.Enabled = true;
                    btnCancel.Visible = true;
                    btnCancel.Text = "Close";
                }));
            }
        }

        private void CreateShortcut(string shortcutPath, string targetExe, string workingDir, string description, string iconPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                
                shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                
                if (!string.IsNullOrEmpty(description))
                    shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { description });
                
                if (!string.IsNullOrEmpty(iconPath))
                    shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                
                shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch (Exception ex)
            {
                LogMessage("Shortcut error (" + Path.GetFileName(shortcutPath) + "): " + ex.Message);
            }
        }

        private void LogMessage(string msg)
        {
            if (this.IsDisposed) return;
            Invoke((Action)(() => {
                if (lstLog != null && !lstLog.IsDisposed)
                {
                    lstLog.Items.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg);
                    lstLog.SelectedIndex = lstLog.Items.Count - 1;
                }
            }));
        }

        private void UpdateProgress(string status, int percent)
        {
            if (this.IsDisposed) return;
            Invoke((Action)(() => {
                if (lblProgressStatus != null && !lblProgressStatus.IsDisposed)
                    lblProgressStatus.Text = status;
                if (progressBar != null && !progressBar.IsDisposed)
                    progressBar.Value = percent;
            }));
        }

        #endregion

        #region UI Button Event Handlers

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            if (currentStep == Step.License)
            {
                SwitchToStep(Step.Options);
            }
            else if (currentStep == Step.Options)
            {
                actualInstallDir = txtPath.Text;
                optDesktop = chkDesktop.Checked;
                optStartMenu = chkStartMenu.Checked;
                optTaskbar = chkTaskbar.Checked;

                StartInstallation();
            }
            else if (currentStep == Step.Success)
            {
                
                if (isUninstallMode)
                {
                    
                    TriggerSelfDestruct();
                }
                else
                {
                    if (chkLaunchApp != null && chkLaunchApp.Checked)
                    {
                        try
                        {
                            string targetPath = Path.Combine(actualInstallDir, exeName);
                            Process.Start(new ProcessStartInfo() {
                                FileName = targetPath,
                                WorkingDirectory = actualInstallDir
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Could not launch application:\n" + ex.Message, "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }

                    if (requireRestart)
                    {
                        Process.Start("shutdown.exe", "/r /t 5 /c \"Restarting to finalize installation\"");
                    }
                }
                Application.Exit();
            }
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            if (currentStep == Step.Options)
            {
                if (!string.IsNullOrEmpty(licenseUrl))
                    SwitchToStep(Step.License);
            }
            else if (currentStep == Step.License)
            {
                
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Installation Directory";
                fbd.SelectedPath = txtPath.Text;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void TriggerSelfDestruct()
        {
            try
            {
                
                
                string cmdArgs = "/c ping 127.0.0.1 -n 2 > nul & del /f /q \"" + Application.ExecutablePath + "\" & rmdir /s /q \"" + actualInstallDir + "\"";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmdArgs);
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch { }
        }

        #endregion

        #region Inline JSON Utility Parser (Regex Based to avoid dependencies)
        private static string GetJsonString(string json, string key, string defaultValue)
        {
            string pattern = "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"";
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Replace("\\\\", "\\").Replace("\\/", "/");
            }
            return defaultValue;
        }

        private static bool GetJsonBool(string json, string key, bool defaultValue)
        {
            string pattern = "\"" + key + "\"\\s*:\\s*(true|false)";
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return bool.Parse(match.Groups[1].Value);
            }
            return defaultValue;
        }
        #endregion
    }

    #region Custom Drawn Custom Controls (Double-Buffered Flat Theme)

    public class FlatButton : Button
    {
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color ActiveColor { get; set; }
        public Color TextColor { get; set; }
        public int BorderRadius { get; set; }

        private bool isHovered = false;
        private bool isPressed = false;

        public FlatButton()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BorderRadius = 6;
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) isPressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { isPressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color currentBg = NormalColor;
            if (isPressed) currentBg = ActiveColor;
            else if (isHovered) currentBg = HoverColor;

            using (GraphicsPath path = GetRoundedRectPath(0, 0, Width, Height, BorderRadius))
            {
                using (SolidBrush brush = new SolidBrush(currentBg))
                {
                    g.FillPath(brush, path);
                }
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, TextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetRoundedRectPath(float x, float y, float width, float height, float radius)
        {
            GraphicsPath gp = new GraphicsPath();
            float d = radius * 2;
            if (d > width) d = width;
            if (d > height) d = height;
            if (d <= 0) { gp.AddRectangle(new RectangleF(x, y, width, height)); return gp; }
            gp.AddArc(x, y, d, d, 180, 90);
            gp.AddArc(x + width - d, y, d, d, 270, 90);
            gp.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            gp.AddArc(x, y + height - d, d, d, 90, 90);
            gp.CloseAllFigures();
            return gp;
        }
    }

    public class FlatCheckbox : CheckBox
    {
        public Color BoxBorderColor { get; set; }
        public Color BoxBgColor { get; set; }
        public Color AccentColor { get; set; }
        public Color TextColor { get; set; }

        private bool isHovered = false;

        public FlatCheckbox()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent.BackColor);

            int boxSize = 16;
            int y = (Height - boxSize) / 2;

            using (GraphicsPath path = GetRoundedRectPath(2, y, boxSize, boxSize, 4))
            {
                if (Checked)
                {
                    using (SolidBrush brush = new SolidBrush(AccentColor))
                    {
                        g.FillPath(brush, path);
                    }
                    using (Pen checkPen = new Pen(Color.White, 2f))
                    {
                        checkPen.StartCap = LineCap.Round;
                        checkPen.EndCap = LineCap.Round;
                        g.DrawLine(checkPen, 5, y + 8, 8, y + 11);
                        g.DrawLine(checkPen, 8, y + 11, 13, y + 4);
                    }
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(BoxBgColor))
                    {
                        g.FillPath(brush, path);
                    }
                    using (Pen pen = new Pen(isHovered ? AccentColor : BoxBorderColor, 1.5f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            Rectangle textRect = new Rectangle(boxSize + 10, 0, Width - boxSize - 10, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private GraphicsPath GetRoundedRectPath(float x, float y, float width, float height, float radius)
        {
            GraphicsPath gp = new GraphicsPath();
            float d = radius * 2;
            if (d > width) d = width;
            if (d > height) d = height;
            if (d <= 0) { gp.AddRectangle(new RectangleF(x, y, width, height)); return gp; }
            gp.AddArc(x, y, d, d, 180, 90);
            gp.AddArc(x + width - d, y, d, d, 270, 90);
            gp.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            gp.AddArc(x, y + height - d, d, d, 90, 90);
            gp.CloseAllFigures();
            return gp;
        }
    }

    public class CustomProgressBar : UserControl
    {
        private int val = 0;
        public int Value
        {
            get { return val; }
            set { val = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public Color ProgressColor1 { get; set; }
        public Color ProgressColor2 { get; set; }
        public Color ProgressBgColor { get; set; }
        public Color BorderColor { get; set; }

        public CustomProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.Size = new Size(200, 14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float r = Height / 2f;
            using (GraphicsPath path = GetRoundedRectPath(0, 0, Width, Height, r))
            {
                using (SolidBrush bgBrush = new SolidBrush(ProgressBgColor))
                {
                    g.FillPath(bgBrush, path);
                }

                if (val > 0)
                {
                    float fillWidth = (Width * val) / 100f;
                    if (fillWidth > Height)
                    {
                        using (GraphicsPath fillPath = GetRoundedRectPath(0, 0, fillWidth, Height, r))
                        {
                            using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                                new PointF(0, 0), new PointF(fillWidth, 0),
                                ProgressColor1, ProgressColor2))
                            {
                                g.FillPath(fillBrush, fillPath);
                            }
                        }
                    }
                    else
                    {
                        using (SolidBrush sBrush = new SolidBrush(ProgressColor1))
                        {
                            g.FillEllipse(sBrush, 0, 0, Height, Height);
                        }
                    }
                }

                using (Pen borderPen = new Pen(BorderColor, 1f))
                {
                    g.DrawPath(borderPen, path);
                }
            }
        }

        private GraphicsPath GetRoundedRectPath(float x, float y, float width, float height, float radius)
        {
            GraphicsPath gp = new GraphicsPath();
            float d = radius * 2;
            if (d > width) d = width;
            if (d > height) d = height;
            if (d <= 0) { gp.AddRectangle(new RectangleF(x, y, width, height)); return gp; }
            gp.AddArc(x, y, d, d, 180, 90);
            gp.AddArc(x + width - d, y, d, d, 270, 90);
            gp.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            gp.AddArc(x, y + height - d, d, d, 90, 90);
            gp.CloseAllFigures();
            return gp;
        }
    }

    #endregion
}
