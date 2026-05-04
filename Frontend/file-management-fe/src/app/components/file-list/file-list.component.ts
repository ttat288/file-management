import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FileService } from '../../services/file.service';
import { FileItem, PagedResult, ApiResponse } from '../../models/file.model';
import { Subject, of } from 'rxjs';
import { takeUntil, debounceTime, distinctUntilChanged, finalize, exhaustMap, catchError, startWith, timeout } from 'rxjs/operators';

@Component({
  selector: 'app-file-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './file-list.component.html',
  styleUrls: ['./file-list.component.scss'],
})
export class FileListComponent implements OnInit, OnDestroy {
  files: FileItem[] = [];
  pagedResult: PagedResult<FileItem> | null = null;
  isLoading = false;
  errorMessage = '';
  successMessage = '';
  currentPage = 1;
  pageSize = 10;
  searchTerm = '';
  renamingFileId: string | null = null;
  newFileName = '';
  deletingFileId: string | null = null;

  previewFile: FileItem | null = null;
  previewUrl: string | null = null;
  openActionsForFileId: string | null = null;

  private destroy$ = new Subject<void>();
  private searchSubject$ = new Subject<string>();
  private refresh$ = new Subject<void>();

  constructor(private fileService: FileService) {}

  ngOnInit(): void {
    this.setupSearch();
    this.setupRequestPipeline();

    this.fileService.filesChanged$.pipe(takeUntil(this.destroy$)).subscribe(() => this.refresh());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Load files from server
   */
  loadFiles(): void {
    this.refresh();
  }

  /**
   * Search files
   */
  onSearch(term: string): void {
    this.searchTerm = term;
    this.currentPage = 1;
    this.searchSubject$.next(term);
  }

  /**
   * Go to previous page
   */
  previousPage(): void {
    if (this.pagedResult?.hasPreviousPage) {
      this.currentPage--;
      this.refresh();
    }
  }

  /**
   * Go to next page
   */
  nextPage(): void {
    if (this.pagedResult?.hasNextPage) {
      this.currentPage++;
      this.refresh();
    }
  }

  /**
   * Start rename operation
   */
  startRename(file: FileItem): void {
    this.renamingFileId = file.id;
    this.newFileName = file.name;
  }

  /**
   * Cancel rename operation
   */
  cancelRename(): void {
    this.renamingFileId = null;
    this.newFileName = '';
  }

  /**
   * Save file rename
   */
  saveRename(fileId: string): void {
    if (!this.newFileName.trim()) {
      this.errorMessage = 'File name cannot be empty';
      return;
    }

    this.fileService
      .renameFile(fileId, this.newFileName.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: ApiResponse<FileItem>) => {
          if (response.success) {
            this.successMessage = 'File renamed successfully';
            this.loadFiles();
            this.renamingFileId = null;
            this.newFileName = '';
            setTimeout(() => (this.successMessage = ''), 3000);
          } else {
            this.errorMessage = response.message || 'Rename failed';
          }
        },
        error: (error) => {
          this.errorMessage = error?.error?.message || 'Error renaming file';
        },
      });
  }

  /**
   * Delete file
   */
  deleteFile(fileId: string, fileName: string): void {
    if (confirm(`Are you sure you want to delete "${fileName}"?`)) {
      this.fileService
        .deleteFile(fileId)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (response: ApiResponse<any>) => {
            if (response.success) {
              this.successMessage = 'File deleted successfully';
              this.refresh();
              setTimeout(() => (this.successMessage = ''), 3000);
            } else {
              this.errorMessage = response.message || 'Delete failed';
            }
          },
          error: (error) => {
            this.errorMessage = error?.error?.message || 'Error deleting file';
          },
        });
    }
  }

  /**
   * Download file (open Blob URL)
   */
  downloadFile(file: FileItem): void {
    this.fileService.getFileUrl(file.id, 60, true).pipe(takeUntil(this.destroy$)).subscribe({
      next: (response) => {
        if (response.success && response.data) window.location.assign(response.data);
        else this.errorMessage = response.message || 'Failed to create download URL';
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || error?.message || 'Failed to create download URL';
      },
    });
  }

  toggleActions(fileId: string): void {
    this.openActionsForFileId = this.openActionsForFileId === fileId ? null : fileId;
  }

  closeActions(): void {
    this.openActionsForFileId = null;
  }

  openPreview(file: FileItem): void {
    if (!this.isImage(file)) return;
    this.previewFile = file;
    this.previewUrl = null;

    this.fileService.getFileUrl(file.id, 30).pipe(takeUntil(this.destroy$)).subscribe({
      next: (response) => {
        if (response.success && response.data) this.previewUrl = response.data;
        else this.errorMessage = response.message || 'Failed to create preview URL';
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || error?.message || 'Failed to create preview URL';
      },
    });
  }

  closePreview(): void {
    this.previewFile = null;
    this.previewUrl = null;
  }

  isImage(file: FileItem): boolean {
    return !!file.contentType && file.contentType.startsWith('image/');
  }

  /**
   * Format file size for display
   */
  formatFileSize(bytes: number): string {
    return this.fileService.formatFileSize(bytes);
  }

  /**
   * Format date for display
   */
  formatDate(date: Date | string): string {
    const d = new Date(date);
    return d.toLocaleDateString() + ' ' + d.toLocaleTimeString();
  }

  /**
   * Get file icon based on content type
   */
  getFileIcon(contentType: string): string {
    if (contentType.startsWith('image/')) return '🖼️';
    if (contentType.includes('pdf')) return '📄';
    if (contentType.startsWith('video/')) return '🎥';
    if (contentType.startsWith('audio/')) return '🎵';
    return '📎';
  }

  // Private helper methods

  private setupSearch(): void {
    this.searchSubject$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.refresh();
      });
  }

  private refresh(): void {
    this.refresh$.next();
  }

  private setupRequestPipeline(): void {
    // Single source of truth for list/search requests; ensures deterministic loading lifecycle.
    this.refresh$
      .pipe(
        startWith(void 0),
        takeUntil(this.destroy$),
        // Don't cancel in-flight requests; ignore extra triggers until current request completes.
        exhaustMap(() => {
          const term = this.searchTerm.trim();
          const page = this.currentPage;
          const size = this.pageSize;

          this.isLoading = true;
          this.errorMessage = '';

          const request$ = term
            ? this.fileService.searchFiles(term, undefined, page, size)
            : this.fileService.getFiles(undefined, page, size);

          return request$.pipe(
            timeout(8000),
            catchError((error) => {
              this.errorMessage = error?.error?.message || error?.message || (term ? 'Error searching files' : 'Error loading files');
              return of(null);
            }),
            finalize(() => {
              this.isLoading = false;
            }),
          );
        }),
      )
      .subscribe((response: ApiResponse<PagedResult<FileItem>> | null) => {
        if (!response) return;

        if (response.success && response.data) {
          this.pagedResult = response.data;
          this.files = response.data.items ?? [];
          return;
        }

        this.errorMessage = response.message || (this.searchTerm.trim() ? 'Search failed' : 'Failed to load files');
      });
  }
}
