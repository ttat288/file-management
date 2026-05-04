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
import { FileItem, PagedResult, ApiResponse } from '../../models/file.model';
import { FolderItem } from '../../models/folder.model';

type ModalState =
  | { kind: 'none' }
  | { kind: 'new_folder' }
  | { kind: 'rename_file'; file: FileItem }
  | { kind: 'rename_folder'; folder: FolderItem }
  | { kind: 'preview'; file: FileItem; url?: string | null };

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
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
  uploading = false;
  isDragging = false;

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

    // Connect after refresh attempt
    setTimeout(() => this.events.connect(() => this.auth.getAccessToken(), (e) => this.onRealtimeEvent(e)), 0);
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
    if (!confirm(`Delete folder "${folder.name}"? This will also remove subfolders.`)) return;
    this.foldersApi.deleteFolder(folder.id).subscribe({
      next: (res) => {
        if (!res.success) this.error = res.message || 'Failed to delete folder';
        this.load();
      },
      error: (e) => (this.error = e?.message || 'Failed to delete folder'),
    });
  }

  deleteFile(file: FileItem): void {
    if (!confirm(`Delete file "${file.name}"?`)) return;
    this.filesApi.deleteFile(file.id).subscribe({
      next: (res: ApiResponse<any>) => {
        if (!res.success) this.error = res.message || 'Failed to delete file';
        this.load();
      },
      error: (e) => (this.error = e?.message || 'Failed to delete file'),
    });
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

  isPreviewable(file: FileItem): boolean {
    const ct = file.contentType || '';
    return ct.startsWith('image/') || ct.startsWith('video/') || ct.includes('pdf');
  }

  onFileInput(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.upload(file);
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
    const file = ev.dataTransfer?.files?.[0];
    if (file) this.upload(file);
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

    const folderId = this.currentFolderId;
    const term = this.search.trim();

    this.foldersApi.getFolders(folderId).subscribe({
      next: (res) => {
        if (res.success && res.data) this.folders = res.data.items ?? [];
      },
      error: () => {},
    });

    const req$ = term ? this.filesApi.searchFiles(term, folderId || undefined, 1, 100) : this.filesApi.getFiles(folderId || undefined, 1, 100);
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
    if (e.type.endsWith('_created') || e.type.endsWith('_deleted') || e.type.endsWith('_renamed')) this.load();
  }

  @HostListener('document:keydown.escape')
  onEsc(): void {
    if (this.modal.kind !== 'none') this.closeModal();
  }
}
