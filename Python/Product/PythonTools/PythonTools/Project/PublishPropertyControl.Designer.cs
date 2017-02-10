namespace Microsoft.PythonTools.Project {
    partial class PublishPropertyControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PublishPropertyControl));
            this._publishLocationGroupBox = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this._publishLocationLabel = new System.Windows.Forms.Label();
            this._pubUrl = new System.Windows.Forms.TextBox();
            this._pubNowButton = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this._publishLocationGroupBox.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // _publishLocationGroupBox
            // 
            resources.ApplyResources(this._publishLocationGroupBox, "_publishLocationGroupBox");
            this._publishLocationGroupBox.Controls.Add(this.tableLayoutPanel2);
            this._publishLocationGroupBox.Name = "_publishLocationGroupBox";
            this._publishLocationGroupBox.TabStop = false;
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(this.tableLayoutPanel2, "tableLayoutPanel2");
            this.tableLayoutPanel2.Controls.Add(this._publishLocationLabel, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this._pubUrl, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this._pubNowButton, 1, 3);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _publishLocationLabel
            // 
            resources.ApplyResources(this._publishLocationLabel, "_publishLocationLabel");
            this._publishLocationLabel.AutoEllipsis = true;
            this.tableLayoutPanel2.SetColumnSpan(this._publishLocationLabel, 2);
            this._publishLocationLabel.Name = "_publishLocationLabel";
            // 
            // _pubUrl
            // 
            this.tableLayoutPanel2.SetColumnSpan(this._pubUrl, 2);
            resources.ApplyResources(this._pubUrl, "_pubUrl");
            this._pubUrl.Name = "_pubUrl";
            this._pubUrl.TextChanged += new System.EventHandler(this._pubUrl_TextChanged);
            // 
            // _pubNowButton
            // 
            resources.ApplyResources(this._pubNowButton, "_pubNowButton");
            this._pubNowButton.Name = "_pubNowButton";
            this._pubNowButton.UseVisualStyleBackColor = true;
            this._pubNowButton.Click += new System.EventHandler(this._pubNowButton_Click);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this._publishLocationGroupBox, 0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // PublishPropertyControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PublishPropertyControl";
            this._publishLocationGroupBox.ResumeLayout(false);
            this._publishLocationGroupBox.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _publishLocationGroupBox;
        private System.Windows.Forms.TextBox _pubUrl;
        private System.Windows.Forms.Label _publishLocationLabel;
        private System.Windows.Forms.Button _pubNowButton;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}
