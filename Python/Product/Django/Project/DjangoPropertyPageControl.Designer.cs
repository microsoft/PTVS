namespace Microsoft.PythonTools.Django.Project {
    partial class DjangoPropertyPageControl {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DjangoPropertyPageControl));
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            this._settingsModuleLabel = new System.Windows.Forms.Label();
            this._settingsModule = new System.Windows.Forms.TextBox();
            this._staticUriLabel = new System.Windows.Forms.Label();
            this._staticUri = new System.Windows.Forms.TextBox();
            this._djangoGroup = new System.Windows.Forms.GroupBox();
            this._deprecatedLabel = new System.Windows.Forms.Label();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this._errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel2.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            this._djangoGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._errorProvider)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(tableLayoutPanel2, "tableLayoutPanel2");
            tableLayoutPanel2.Controls.Add(this._settingsModuleLabel, 0, 0);
            tableLayoutPanel2.Controls.Add(this._settingsModule, 1, 0);
            tableLayoutPanel2.Controls.Add(this._staticUriLabel, 0, 1);
            tableLayoutPanel2.Controls.Add(this._staticUri, 1, 1);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _settingsModuleLabel
            // 
            resources.ApplyResources(this._settingsModuleLabel, "_settingsModuleLabel");
            this._settingsModuleLabel.Name = "_settingsModuleLabel";
            // 
            // _settingsModule
            // 
            resources.ApplyResources(this._settingsModule, "_settingsModule");
            this._settingsModule.Name = "_settingsModule";
            this._settingsModule.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _staticUriLabel
            // 
            resources.ApplyResources(this._staticUriLabel, "_staticUriLabel");
            this._staticUriLabel.Name = "_staticUriLabel";
            // 
            // _staticUri
            // 
            resources.ApplyResources(this._staticUri, "_staticUri");
            this._staticUri.Name = "_staticUri";
            this._staticUri.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(this._djangoGroup, 0, 0);
            tableLayoutPanel1.Controls.Add(this._deprecatedLabel, 0, 1);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _djangoGroup
            // 
            resources.ApplyResources(this._djangoGroup, "_djangoGroup");
            this._djangoGroup.Controls.Add(tableLayoutPanel2);
            this._djangoGroup.Name = "_djangoGroup";
            this._djangoGroup.TabStop = false;
            // 
            // _deprecatedLabel
            // 
            resources.ApplyResources(this._deprecatedLabel, "_deprecatedLabel");
            this._errorProvider.SetError(this._deprecatedLabel, resources.GetString("_deprecatedLabel.Error"));
            this._errorProvider.SetIconAlignment(this._deprecatedLabel, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("_deprecatedLabel.IconAlignment"))));
            this._deprecatedLabel.Name = "_deprecatedLabel";
            // 
            // _errorProvider
            // 
            this._errorProvider.ContainerControl = this;
            resources.ApplyResources(this._errorProvider, "_errorProvider");
            // 
            // DjangoPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(tableLayoutPanel1);
            this.Name = "DjangoPropertyPageControl";
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel2.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this._djangoGroup.ResumeLayout(false);
            this._djangoGroup.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._errorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _djangoGroup;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Label _settingsModuleLabel;
        private System.Windows.Forms.TextBox _settingsModule;
        private System.Windows.Forms.Label _staticUriLabel;
        private System.Windows.Forms.TextBox _staticUri;
        private System.Windows.Forms.ErrorProvider _errorProvider;
        private System.Windows.Forms.Label _deprecatedLabel;
    }
}
