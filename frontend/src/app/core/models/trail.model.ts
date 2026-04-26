export interface TrailResponse {
  id: number;
  name: string;
  description: string;
  icon: string;
  accentColor: string;
  level: string;
  totalContents: number;
  userProgress: number; // -1 = não inscrito, 0-100 = progresso
}

export interface ContentResponse {
  id: number;
  trailId: number;
  title: string;
  body: string;
  externalUrl: string | null;
  type: string;
  level: string;
  order: number;
  isCompleted: boolean;
}

export interface ProgressResponse {
  trailId: number;
  trailName: string;
  progress: number;
  completedContents: number;
  totalContents: number;
  isCompleted: boolean;
}
