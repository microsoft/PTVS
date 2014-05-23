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
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
            System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PythonWebPropertyPageControl));
            this._wsgiHandlerLabel = new System.Windows.Forms.Label();
            this._wsgiHandler = new System.Windows.Forms.TextBox();
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
            tableLayoutPanel2.AutoSize = true;
            tableLayoutPanel2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel2.ColumnCount = 2;
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel2.Controls.Add(this._wsgiHandlerLabel, 0, 0);
            tableLayoutPanel2.Controls.Add(this._wsgiHandler, 1, 0);
            tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel2.Location = new System.Drawing.Point(6, 21);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 1;
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel2.Size = new System.Drawing.Size(394, 26);
            tableLayoutPanel2.TabIndex = 0;
            // 
            // _wsgiHandlerLabel
            // 
            this._wsgiHandlerLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._wsgiHandlerLabel.AutoSize = true;
            this._wsgiHandlerLabel.Location = new System.Drawing.Point(6, 6);
            this._wsgiHandlerLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._wsgiHandlerLabel.Name = "_wsgiHandlerLabel";
            this._wsgiHandlerLabel.Size = new System.Drawing.Size(79, 13);
            this._wsgiHandlerLabel.TabIndex = 2;
            this._wsgiHandlerLabel.Text = "&WSGI Handler:";
            this._wsgiHandlerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _wsgiHandler
            // 
            this._wsgiHandler.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._wsgiHandler.Location = new System.Drawing.Point(97, 3);
            this._wsgiHandler.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._wsgiHandler.MinimumSize = new System.Drawing.Size(50, 4);
            this._wsgiHandler.Name = "_wsgiHandler";
            this._wsgiHandler.Size = new System.Drawing.Size(291, 20);
            this._wsgiHandler.TabIndex = 3;
            this._wsgiHandler.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.AutoSize = true;
            tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(this.groupBox1, 0, 1);
            tableLayoutPanel1.Controls.Add(this._webGroup, 0, 0);
            tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            tableLayoutPanel1.Size = new System.Drawing.Size(418, 188);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.AutoSize = true;
            this.groupBox1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupBox1.Controls.Add(tableLayoutPanel3);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(6, 79);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.groupBox1.Size = new System.Drawing.Size(406, 81);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Static Files";
            // 
            // tableLayoutPanel3
            // 
            tableLayoutPanel3.AutoSize = true;
            tableLayoutPanel3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            tableLayoutPanel3.ColumnCount = 2;
            tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel3.Controls.Add(this._staticPatternLabel, 0, 0);
            tableLayoutPanel3.Controls.Add(this._staticPattern, 1, 0);
            tableLayoutPanel3.Controls.Add(this._staticRewriteLabel, 0, 1);
            tableLayoutPanel3.Controls.Add(this._staticRewrite, 1, 1);
            tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel3.Location = new System.Drawing.Point(6, 21);
            tableLayoutPanel3.Name = "tableLayoutPanel3";
            tableLayoutPanel3.RowCount = 2;
            tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            tableLayoutPanel3.Size = new System.Drawing.Size(394, 52);
            tableLayoutPanel3.TabIndex = 0;
            // 
            // _staticPatternLabel
            // 
            this._staticPatternLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._staticPatternLabel.AutoSize = true;
            this._staticPatternLabel.Location = new System.Drawing.Point(6, 6);
            this._staticPatternLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._staticPatternLabel.Name = "_staticPatternLabel";
            this._staticPatternLabel.Size = new System.Drawing.Size(66, 13);
            this._staticPatternLabel.TabIndex = 0;
            this._staticPatternLabel.Text = "&URI Pattern:";
            this._staticPatternLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _staticPattern
            // 
            this._staticPattern.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._errorProvider.SetIconPadding(this._staticPattern, -20);
            this._staticPattern.Location = new System.Drawing.Point(86, 3);
            this._staticPattern.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._staticPattern.MinimumSize = new System.Drawing.Size(50, 4);
            this._staticPattern.Name = "_staticPattern";
            this._staticPattern.Size = new System.Drawing.Size(302, 20);
            this._staticPattern.TabIndex = 1;
            this._staticPattern.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _staticRewriteLabel
            // 
            this._staticRewriteLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._staticRewriteLabel.AutoSize = true;
            this._staticRewriteLabel.Location = new System.Drawing.Point(6, 32);
            this._staticRewriteLabel.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this._staticRewriteLabel.Name = "_staticRewriteLabel";
            this._staticRewriteLabel.Size = new System.Drawing.Size(68, 13);
            this._staticRewriteLabel.TabIndex = 2;
            this._staticRewriteLabel.Text = "URI Re&write:";
            this._staticRewriteLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _staticRewrite
            // 
            this._staticRewrite.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._staticRewrite.Location = new System.Drawing.Point(86, 29);
            this._staticRewrite.Margin = new System.Windows.Forms.Padding(6, 3, 6, 3);
            this._staticRewrite.MinimumSize = new System.Drawing.Size(50, 4);
            this._staticRewrite.Name = "_staticRewrite";
            this._staticRewrite.Size = new System.Drawing.Size(302, 20);
            this._staticRewrite.TabIndex = 3;
            this._staticRewrite.TextChanged += new System.EventHandler(this.Setting_TextChanged);
            // 
            // _webGroup
            // 
            this._webGroup.AutoSize = true;
            this._webGroup.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._webGroup.Controls.Add(tableLayoutPanel2);
            this._webGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._webGroup.Location = new System.Drawing.Point(6, 8);
            this._webGroup.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._webGroup.Name = "_webGroup";
            this._webGroup.Padding = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this._webGroup.Size = new System.Drawing.Size(406, 55);
            this._webGroup.TabIndex = 0;
            this._webGroup.TabStop = false;
            this._webGroup.Text = "Web";
            // 
            // _errorProvider
            // 
            this._errorProvider.ContainerControl = this;
            this._errorProvider.Icon = ((System.Drawing.Icon)(resources.GetObject("_errorProvider.Icon")));
            // 
            // PythonWebPropertyPageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(tableLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "PythonWebPropertyPageControl";
            this.Size = new System.Drawing.Size(418, 188);
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
    }
}
