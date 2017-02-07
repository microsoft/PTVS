namespace Microsoft.PythonTools.Project.Web {
    partial class PythonWebPropertyPageControl {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonWebPropertyPageControl));
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
            this._wsgiHandlerLabel = new System.Windows.Forms.Label();
            this._wsgiHandler = new System.Windows.Forms.TextBox();
            this._deprecatedLabel = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this._staticPatternLabel = new System.Windows.Forms.Label();
            this._staticPattern = new System.Windows.Forms.TextBox();
            this._staticRewriteLabel = new System.Windows.Forms.Label();
            this._staticRewrite = new System.Windows.Forms.TextBox();
            this._webGroup = new System.Windows.Forms.GroupBox();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this._errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            tableLayoutPanel2.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            tableLayoutPanel3.SuspendLayout();
            this._webGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._errorProvider)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel2
            // 
            resources.ApplyResources(tableLayoutPanel2, "tableLayoutPanel2");
            tableLayoutPanel2.Controls.Add(this._wsgiHandlerLabel, 0, 0);
            tableLayoutPanel2.Controls.Add(this._wsgiHandler, 1, 0);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            // 
            // _wsgiHandlerLabel
            // 
            resources.ApplyResources(this._wsgiHandlerLabel, "_wsgiHandlerLabel");
            this._wsgiHandlerLabel.Name = "_wsgiHandlerLabel";
            // 
            // _wsgiHandler
            // 
            resources.ApplyResources(this._wsgiHandler, "_wsgiHandler");
            this._wsgiHandler.Name = "_wsgiHandler";
            this._wsgiHandler.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(tableLayoutPanel1, "tableLayoutPanel1");
            tableLayoutPanel1.Controls.Add(this._deprecatedLabel, 0, 0);
            tableLayoutPanel1.Controls.Add(this.groupBox1, 0, 2);
            tableLayoutPanel1.Controls.Add(this._webGroup, 0, 1);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // _deprecatedLabel
            // 
            resources.ApplyResources(this._deprecatedLabel, "_deprecatedLabel");
            this._errorProvider.SetError(this._deprecatedLabel, resources.GetString("_deprecatedLabel.Error"));
            this._errorProvider.SetIconAlignment(this._deprecatedLabel, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("_deprecatedLabel.IconAlignment"))));
            this._deprecatedLabel.Name = "_deprecatedLabel";
            // 
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Controls.Add(tableLayoutPanel3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // tableLayoutPanel3
            // 
            resources.ApplyResources(tableLayoutPanel3, "tableLayoutPanel3");
            tableLayoutPanel3.Controls.Add(this._staticPatternLabel, 0, 0);
            tableLayoutPanel3.Controls.Add(this._staticPattern, 1, 0);
            tableLayoutPanel3.Controls.Add(this._staticRewriteLabel, 0, 1);
            tableLayoutPanel3.Controls.Add(this._staticRewrite, 1, 1);
            tableLayoutPanel3.Name = "tableLayoutPanel3";
            // 
            // _staticPatternLabel
            // 
            resources.ApplyResources(this._staticPatternLabel, "_staticPatternLabel");
            this._staticPatternLabel.Name = "_staticPatternLabel";
            // 
            // _staticPattern
            // 
            resources.ApplyResources(this._staticPattern, "_staticPattern");
            this._errorProvider.SetIconPadding(this._staticPattern, ((int)(resources.GetObject("_staticPattern.IconPadding"))));
            this._staticPattern.Name = "_staticPattern";
            this._staticPattern.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _staticRewriteLabel
            // 
            resources.ApplyResources(this._staticRewriteLabel, "_staticRewriteLabel");
            this._staticRewriteLabel.Name = "_staticRewriteLabel";
            // 
            // _staticRewrite
            // 
            resources.ApplyResources(this._staticRewrite, "_staticRewrite");
            this._staticRewrite.Name = "_staticRewrite";
            this._staticRewrite.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _webGroup
            // 
            resources.ApplyResources(this._webGroup, "_webGroup");
            this._webGroup.Controls.Add(tableLayoutPanel2);
            this._webGroup.Name = "_webGroup";
            this._webGroup.TabStop = false;
            // 
            // _errorProvider
            // 
            this._errorProvider.ContainerControl = this;
            resources.ApplyResources(this._errorProvider, "_errorProvider");
            // 
            // PythonWebPropertyPageControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(tableLayoutPanel1);
            this.Name = "PythonWebPropertyPageControl";
            tableLayoutPanel2.ResumeLayout(false);
            tableLayoutPanel2.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            tableLayoutPanel3.ResumeLayout(false);
            tableLayoutPanel3.PerformLayout();
            this._webGroup.ResumeLayout(false);
            this._webGroup.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._errorProvider)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox _webGroup;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Label _wsgiHandlerLabel;
        private System.Windows.Forms.TextBox _wsgiHandler;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label _staticPatternLabel;
        private System.Windows.Forms.TextBox _staticPattern;
        private System.Windows.Forms.ErrorProvider _errorProvider;
        private System.Windows.Forms.Label _staticRewriteLabel;
        private System.Windows.Forms.TextBox _staticRewrite;
        private System.Windows.Forms.Label _deprecatedLabel;
    }
}
