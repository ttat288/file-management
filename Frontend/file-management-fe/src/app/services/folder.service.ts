import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiResponse, PagedResult } from '../models/file.model';
import { FolderItem } from '../models/folder.model';

@Injectable({ providedIn: 'root' })
export class FolderService {
  private apiUrl = `${environment.apiBaseUrl}/folders`;

  constructor(private http: HttpClient) {}

  getFolders(parentId?: string | null, pageNumber = 1, pageSize = 200): Observable<ApiResponse<PagedResult<FolderItem>>> {
    let url = `${this.apiUrl}?pageNumber=${pageNumber}&pageSize=${pageSize}`;
    if (parentId) url += `&parentId=${parentId}`;
    return this.http.get<ApiResponse<PagedResult<FolderItem>>>(url);
  }

  createFolder(name: string, parentId?: string | null): Observable<ApiResponse<FolderItem>> {
    return this.http.post<ApiResponse<FolderItem>>(this.apiUrl, { name, parentId });
  }

  renameFolder(folderId: string, newName: string): Observable<ApiResponse<FolderItem>> {
    return this.http.put<ApiResponse<FolderItem>>(`${this.apiUrl}/${folderId}/rename`, { newName });
  }

  deleteFolder(folderId: string): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.apiUrl}/${folderId}`);
  }
}

