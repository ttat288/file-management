export interface FolderItem {
  id: string;
  name: string;
  parentId?: string | null;
  createdAt: Date | string;
}

