import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { FileService } from '../../services/file.service';
import { FolderService } from '../../services/folder.service';
import { EventsService, RealtimeEvent } from '../../services/events.service';
import { FileItem, PagedResult, ApiResponse, UploadProgress } from '../../models/file.model';
import { FolderItem } from '../../models/folder.model';
import { NotificationPopupComponent } from '../../components/notification-popup/notification-popup.component';

type PopupDialog = {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  showCancel?: boolean;
  confirmVariant?: 'primary' | 'danger' | 'secondary';
  onConfirm: () => void;
  onCancel?: () => void;
};

type ModalState =
  | { kind: 'none' }
  | { kind: 'new_folder' }
  | { kind: 'rename_file'; file: FileItem }
  | { kind: 'rename_folder'; folder: FolderItem }
  | { kind: 'preview'; file: FileItem; url?: string | null };

type DashboardRow = { kind: 'folder'; data: FolderItem } | { kind: 'file'; data: FileItem };

type ToastMessage = {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info';
  duration: number;
};

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, FormsModule, NotificationPopupComponent],
  templateUrl: './dashboard-page.component.html',
})
export class DashboardPageComponent implements OnInit, OnDestroy {
  folders: FolderItem[] = [];
  files: FileItem[] = [];

  currentFolderId: string | null = null;
  breadcrumb: { id: string | null; name: string }[] = [{ id: null, name: 'Home' }];

  search = '';
  private search$ = new Subject<string>();

  isLoading = false;
  error = '';

  modal: ModalState = { kind: 'none' };
  modalInput = '';
  popup: PopupDialog | null = null;
  uploading = false;
  isDragging = false;

  // New properties for multi-select and batch operations
  selectedFiles = new Set<string>();
  selectedAll = false;
  uploadProgress: UploadProgress[] = [];
  toasts: ToastMessage[] = [];

  private destroy$ = new Subject<void>();

  constructor(
    private auth: AuthService,
    private router: Router,
    private theme: ThemeService,
    private filesApi: FileService,
    private foldersApi: FolderService,
    private events: EventsService,
  ) {}

  ngOnInit(): void {
    this.theme.apply();
    this.auth.refresh().subscribe({ next: () => {} });
    this.load();

    this.search$.pipe(debounceTime(250), takeUntil(this.destroy$)).subscribe(() => this.load());

    this.filesApi.filesChanged$.pipe(takeUntil(this.destroy$)).subscribe(() => this.load());

    // Subscribe to upload progress
    this.filesApi.uploadProgress$.pipe(takeUntil(this.destroy$)).subscribe((progress) => {
      this.uploadProgress = progress;
      this.uploading = progress.some((p) => p.status === 'uploading');
    });

    // Connect after refresh attempt
    setTimeout(
      () =>
        this.events.connect(
          () => this.auth.getAccessToken(),
          (e) => this.onRealtimeEvent(e),
        ),
      0,
    );
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.events.disconnect();
  }

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/login');
  }

  toggleTheme(): void {
    const mode = this.theme.getMode();
    this.theme.setMode(mode === 'dark' ? 'light' : 'dark');
  }

  openFolder(folder: FolderItem): void {
    this.currentFolderId = folder.id;
    this.breadcrumb = [...this.breadcrumb, { id: folder.id, name: folder.name }];
    this.search = '';
    this.load();
  }

  goCrumb(index: number): void {
    const crumb = this.breadcrumb[index];
    this.breadcrumb = this.breadcrumb.slice(0, index + 1);
    this.currentFolderId = crumb.id;
    this.search = '';
    this.load();
  }

  setSearch(v: string): void {
    this.search = v;
    this.search$.next(v);
  }

  openNewFolder(): void {
    this.modalInput = '';
    this.modal = { kind: 'new_folder' };
  }

  openRenameFolder(folder: FolderItem): void {
    this.modalInput = folder.name;
    this.modal = { kind: 'rename_folder', folder };
  }

  openRenameFile(file: FileItem): void {
    this.modalInput = file.name;
    this.modal = { kind: 'rename_file', file };
  }

  closeModal(): void {
    this.modal = { kind: 'none' };
    this.modalInput = '';
  }

  onPopupConfirm(): void {
    if (!this.popup) return;
    const confirmAction = this.popup.onConfirm;
    this.popup = null;
    confirmAction();
  }

  onPopupCancel(): void {
    if (!this.popup) return;
    const cancelAction = this.popup.onCancel;
    this.popup = null;
    if (cancelAction) cancelAction();
  }

  submitModal(): void {
    const value = this.modalInput.trim();
    if (!value) return;

    if (this.modal.kind === 'new_folder') {
      this.foldersApi.createFolder(value, this.currentFolderId).subscribe({
        next: (res) => {
          if (!res.success) this.error = res.message || 'Failed to create folder';
          this.closeModal();
          this.load();
        },
        error: (e) => (this.error = e?.message || 'Failed to create folder'),
      });
      return;
    }

    if (this.modal.kind === 'rename_folder') {
      this.foldersApi.renameFolder(this.modal.folder.id, value).subscribe({
        next: (res) => {
          if (!res.success) this.error = res.message || 'Failed to rename folder';
          this.closeModal();
          this.load();
        },
        error: (e) => (this.error = e?.message || 'Failed to rename folder'),
      });
      return;
    }

    if (this.modal.kind === 'rename_file') {
      this.filesApi.renameFile(this.modal.file.id, value).subscribe({
        next: (res) => {
          if (!res.success) this.error = res.message || 'Failed to rename file';
          this.closeModal();
          this.load();
        },
        error: (e) => (this.error = e?.message || 'Failed to rename file'),
      });
      return;
    }
  }

  deleteFolder(folder: FolderItem): void {
    this.popup = {
      title: 'Delete folder',
      message: `Delete folder "${folder.name}"? This will also remove subfolders.`,
      confirmText: 'Delete',
      cancelText: 'Cancel',
      showCancel: true,
      confirmVariant: 'danger',
      onConfirm: () => {
        this.foldersApi.deleteFolder(folder.id).subscribe({
          next: (res) => {
            if (!res.success) this.error = res.message || 'Failed to delete folder';
            this.load();
          },
          error: (e) => (this.error = e?.message || 'Failed to delete folder'),
        });
      },
    };
  }

  deleteFile(file: FileItem): void {
    this.popup = {
      title: 'Delete file',
      message: `Delete file "${file.name}"?`,
      confirmText: 'Delete',
      cancelText: 'Cancel',
      showCancel: true,
      confirmVariant: 'danger',
      onConfirm: () => {
        this.filesApi.deleteFile(file.id).subscribe({
          next: (res: ApiResponse<any>) => {
            if (!res.success) this.error = res.message || 'Xóa file thất bại';
            else this.showToast(`Xóa thành công "${file.name}"`, 'success');
            this.load();
          },
          error: (e) => {
            this.error = e?.message || 'Xóa file thất bại';
            this.showToast('Xóa file thất bại', 'error');
          },
        });
      },
    };
  }

  downloadFile(file: FileItem): void {
    this.filesApi.getFileUrl(file.id, 60, true).subscribe({
      next: (res) => {
        if (res.success && res.data) window.location.assign(res.data);
        else this.error = res.message || 'Failed to create download URL';
      },
      error: (e) => (this.error = e?.message || 'Failed to download'),
    });
  }

  openPreview(file: FileItem): void {
    this.modal = { kind: 'preview', file, url: null };
    this.filesApi.getFileUrl(file.id, 30, false).subscribe({
      next: (res) => {
        if (this.modal.kind !== 'preview') return;
        if (res.success && res.data) this.modal = { ...this.modal, url: res.data };
        else this.error = res.message || 'Failed to create preview URL';
      },
      error: (e) => (this.error = e?.message || 'Failed to create preview URL'),
    });
  }

  get rows(): DashboardRow[] {
    return [
      ...this.folders.map((folder) => ({ kind: 'folder' as const, data: folder })),
      ...this.files.map((file) => ({ kind: 'file' as const, data: file })),
    ];
  }

  get rowCount(): number {
    return this.rows.length;
  }

  isPreviewable(file: FileItem): boolean {
    const ct = file.contentType || '';
    return ct.startsWith('image/') || ct.startsWith('video/') || ct.includes('pdf');
  }

  isImage(file: FileItem): boolean {
    const ct = file.contentType || '';
    return ct.startsWith('image/');
  }

  isRowClickable(row: DashboardRow): boolean {
    return row.kind === 'folder' || (row.kind === 'file' && this.isImage(row.data));
  }

  getRowTypeLabel(row: DashboardRow): string {
    if (row.kind === 'folder') return 'Folder';
    if (this.isImage(row.data)) return 'Image';
    return 'File';
  }

  getRowActionTitle(row: DashboardRow): string {
    return row.kind === 'folder'
      ? 'Open folder'
      : this.isImage(row.data)
        ? 'Preview image'
        : 'No action available';
  }

  onRowNameClick(row: DashboardRow): void {
    if (row.kind === 'folder') {
      this.openFolder(row.data);
      return;
    }

    if (row.kind === 'file' && this.isImage(row.data)) {
      this.openPreview(row.data);
    }
  }

  onFileInput(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const files = Array.from(input.files || []);
    if (files.length > 0) this.uploadBatch(files);
    input.value = '';
  }

  onDragOver(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.isDragging = true;
  }

  onDragLeave(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.isDragging = false;
  }

  onDrop(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.isDragging = false;
    const files = Array.from(ev.dataTransfer?.files || []);
    if (files.length > 0) this.uploadBatch(files);
  }

  // Multi-select methods
  toggleSelectAll(): void {
    if (this.selectedAll) {
      this.selectedFiles.clear();
    } else {
      this.files.forEach((file) => this.selectedFiles.add(file.id));
    }
    this.selectedAll = !this.selectedAll;
  }

  toggleSelectFile(fileId: string): void {
    if (this.selectedFiles.has(fileId)) {
      this.selectedFiles.delete(fileId);
    } else {
      this.selectedFiles.add(fileId);
    }
    this.selectedAll = this.selectedFiles.size === this.files.length;
  }

  isSelected(fileId: string): boolean {
    return this.selectedFiles.has(fileId);
  }

  getSelectedCount(): number {
    return this.selectedFiles.size;
  }

  clearSelection(): void {
    this.selectedFiles.clear();
    this.selectedAll = false;
  }

  // Batch delete
  deleteSelectedFiles(): void {
    const selectedIds = Array.from(this.selectedFiles);
    if (selectedIds.length === 0) return;

    this.popup = {
      title: 'Delete files',
      message: `Delete ${selectedIds.length} selected file(s)?`,
      confirmText: 'Delete',
      cancelText: 'Cancel',
      showCancel: true,
      confirmVariant: 'danger',
      onConfirm: () => {
        this.performBatchDelete(selectedIds);
      },
    };
  }

  private performBatchDelete(fileIds: string[]): void {
    this.error = '';
    let completed = 0;
    let errors = 0;

    fileIds.forEach((id) => {
      this.filesApi.deleteFile(id).subscribe({
        next: (res: ApiResponse<any>) => {
          completed++;
          if (!res.success) {
            errors++;
            this.error = res.message || 'Failed to delete some files';
          }
          if (completed === fileIds.length) {
            this.clearSelection();
            this.load();
            if (errors === 0) {
              this.showToast(`Xóa thành công ${fileIds.length} files`, 'success');
            } else if (errors < fileIds.length) {
              this.showToast(
                `Đã xóa ${fileIds.length - errors} files, ${errors} thất bại`,
                'error',
              );
            } else {
              this.showToast('Xóa file thất bại', 'error');
            }
          }
        },
        error: (e) => {
          completed++;
          errors++;
          if (completed === fileIds.length) {
            this.clearSelection();
            this.load();
            this.showToast('Some files failed to delete', 'error');
          }
        },
      });
    });
  }

  // Batch upload
  private uploadBatch(files: File[]): void {
    this.error = '';
    this.uploading = true;
    this.filesApi.clearUploadProgress();

    this.filesApi.uploadFileDirectBatch(files, this.currentFolderId).subscribe({
      next: () => {
        this.uploading = false;
        const successMessage =
          files.length === 1
            ? `Upload thành công ${files[0].name}`
            : `Upload thành công ${files.length} files`;
        this.showToast(successMessage, 'success');
        // Clear completed uploads after a delay
        setTimeout(() => this.filesApi.clearCompletedUploads(), 3000);
        this.load();
      },
      error: (e) => {
        this.uploading = false;
        this.showToast('Upload một số file thất bại', 'error');
        setTimeout(() => this.filesApi.clearCompletedUploads(), 5000);
        this.load();
      },
    });
  }

  // Toast notifications
  private showToast(
    message: string,
    type: 'success' | 'error' | 'info' = 'info',
    duration = 3000,
  ): void {
    const toast: ToastMessage = {
      id: Date.now().toString(),
      message,
      type,
      duration,
    };
    this.toasts.push(toast);
    setTimeout(() => {
      this.toasts = this.toasts.filter((t) => t.id !== toast.id);
    }, duration);
  }

  removeToast(id: string): void {
    this.toasts = this.toasts.filter((t) => t.id !== id);
  }

  private upload(file: File): void {
    this.error = '';
    this.uploading = true;

    this.filesApi.uploadFileDirect(file, this.currentFolderId).subscribe({
      next: (event) => {
        if ((event as any).type === 4) this.uploading = false;
      },
      error: (e) => {
        this.uploading = false;
        this.error = e?.message || 'Upload failed';
      },
    });
  }

  private load(): void {
    this.isLoading = true;
    this.error = '';
    this.clearSelection(); // Clear selection when loading new data

    const folderId = this.currentFolderId;
    const term = this.search.trim();

    this.foldersApi.getFolders(folderId).subscribe({
      next: (res) => {
        if (res.success && res.data) this.folders = res.data.items ?? [];
      },
      error: () => {},
    });

    const req$ = term
      ? this.filesApi.searchFiles(term, folderId || undefined, 1, 100)
      : this.filesApi.getFiles(folderId || undefined, 1, 100);
    req$.subscribe({
      next: (res: ApiResponse<PagedResult<FileItem>>) => {
        this.isLoading = false;
        if (res.success && res.data) this.files = res.data.items ?? [];
        else this.error = res.message || 'Failed to load files';
      },
      error: (e) => {
        this.isLoading = false;
        this.error = e?.message || 'Failed to load files';
      },
    });
  }

  private onRealtimeEvent(e: RealtimeEvent): void {
    if (e.type.endsWith('_created') || e.type.endsWith('_deleted') || e.type.endsWith('_renamed'))
      this.load();
  }

  @HostListener('document:keydown.escape')
  onEsc(): void {
    if (this.modal.kind !== 'none') this.closeModal();
  }
}
