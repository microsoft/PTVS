/* 
 * ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 * for more information.
 *
 * ***************************************************************************/


namespace Microsoft.Samples {
    partial class InstallPrompt {
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this._ok = new System.Windows.Forms.Button();
            this._cancel = new System.Windows.Forms.Button();
            this._prompt = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // _ok
            // 
            this._ok.Location = new System.Drawing.Point(306, 58);
            this._ok.Name = "_ok";
            this._ok.Size = new System.Drawing.Size(75, 23);
            this._ok.TabIndex = 0;
            this._ok.Text = "Ok";
            this._ok.UseVisualStyleBackColor = true;
            this._ok.Click += new System.EventHandler(this._ok_Click);
            // 
            // _cancel
            // 
            this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancel.Location = new System.Drawing.Point(387, 58);
            this._cancel.Name = "_cancel";
            this._cancel.Size = new System.Drawing.Size(75, 23);
            this._cancel.TabIndex = 1;
            this._cancel.Text = "Cancel";
            this._cancel.UseVisualStyleBackColor = true;
            this._cancel.Click += new System.EventHandler(this._cancel_Click);
            // 
            // _prompt
            // 
            this._prompt.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._prompt.Location = new System.Drawing.Point(12, 12);
            this._prompt.Multiline = true;
            this._prompt.Name = "_prompt";
            this._prompt.ReadOnly = true;
            this._prompt.Size = new System.Drawing.Size(443, 40);
            this._prompt.TabIndex = 8;
            this._prompt.Text = "Install {0} into {1}?";
            // 
            // InstallPrompt
            // 
            this.AcceptButton = this._ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._cancel;
            this.ClientSize = new System.Drawing.Size(467, 86);
            this.Controls.Add(this._prompt);
            this.Controls.Add(this._cancel);
            this.Controls.Add(this._ok);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "InstallPrompt";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Install Sample?";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button _ok;
        private System.Windows.Forms.Button _cancel;
        private System.Windows.Forms.TextBox _prompt;
    }
}