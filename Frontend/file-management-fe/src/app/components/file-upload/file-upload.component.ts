import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FileService } from '../../services/file.service';
import { UploadProgress, FileItem, ApiResponse } from '../../models/file.model';
import { HttpEventType } from '@angular/common/http';

@Component({
  selector: 'app-file-upload',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './file-upload.component.html',
  styleUrls: ['./file-upload.component.scss'],
})
export class FileUploadComponent implements OnInit {
  uploadProgress: UploadProgress[] = [];
  isDragging = false;
  selectedFile: File | null = null;
  errorMessage = '';
  successMessage = '';
  isUploading = false;

  constructor(private fileService: FileService) {}

  ngOnInit(): void {
    this.fileService.uploadProgress$.subscribe((progress) => {
      this.uploadProgress = progress;
    });
  }

  /**
   * Handle file selection from input
   */
  onFileSelected(event: Event): void {
    const target = event.target as HTMLInputElement;
    const files = target.files;

    if (files && files.length > 0) {
      this.handleFiles(files);
    }
  }

  /**
   * Handle drag and drop
   */
  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = true;
  }

  /**
   * Handle drag leave
   */
  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;
  }

  /**
   * Handle drop
   */
  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;

    const files = event.dataTransfer?.files;
    if (files) {
      this.handleFiles(files);
    }
  }

  /**
   * Upload selected file(s)
   */
  uploadFiles(): void {
    if (!this.selectedFile) {
      this.errorMessage = 'Please select a file first';
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.isUploading = true;

    this.fileService.uploadFile(this.selectedFile).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.Response) {
          const response = event.body as ApiResponse<FileItem> | null;
          if (response?.success) {
            this.successMessage = `File "${this.selectedFile!.name}" uploaded successfully!`;
            this.selectedFile = null;
            const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
            if (fileInput) fileInput.value = '';
            setTimeout(() => this.fileService.clearUploadProgress(), 1500);
          } else {
            this.errorMessage = response?.message || 'Upload failed';
          }
          this.isUploading = false;
        }
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || error?.message || 'An error occurred during upload';
        this.isUploading = false;
      },
    });
  }

  /**
   * Cancel upload
   */
  cancelUpload(): void {
    this.selectedFile = null;
    this.fileService.clearUploadProgress();
    this.errorMessage = '';
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    if (fileInput) fileInput.value = '';
  }

  /**
   * Get progress percentage for display
   */
  getProgressPercentage(): number {
    if (!this.uploadProgress.length) return 0;
    return this.uploadProgress[0].progress || 0;
  }

  /**
   * Check if upload is in progress
   */
  isUploadInProgress(): boolean {
    return this.uploadProgress.some((p) => p.status === 'uploading');
  }

  /**
   * Format file size for display
   */
  formatFileSize(bytes: number): string {
    return this.fileService.formatFileSize(bytes);
  }

  // Private helper methods

  private handleFiles(files: FileList): void {
    if (files.length === 0) return;

    const file = files[0];

    // Validate file size (10GB max)
    const maxSize = 10 * 1024 * 1024 * 1024;
    if (file.size > maxSize) {
      this.errorMessage = 'File size exceeds 10GB limit';
      return;
    }

    this.selectedFile = file;
    this.errorMessage = '';
    this.successMessage = '';
  }
}
