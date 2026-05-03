export interface BadgeInfo {
  id: number;
  name: string;
  description: string;
  icon: string;
  category: string;
  earnedAt: string;
}

export interface CosmeticInfo {
  id: number;
  name: string;
  type: string;
  rarity: string;
  isEquipped: boolean;
}

export interface TrailProgressInfo {
  trailId: number;
  trailName: string;
  progress: number;
  isCompleted: boolean;
}

export interface StudentProfile {
  id: string;
  name: string;
  email: string;
  role: "Student";
  xp: number;
  coins: number;
  stars: number;
  lives: number;
  streakDays: number;
  longestStreak: number;
  loginCycleDay: number;
  activeTitle: string | null;
  badges: BadgeInfo[];
  cosmetics: CosmeticInfo[];
  trailProgress: TrailProgressInfo[];
}

export interface PlatformMetrics {
  totalStudents: number;
  totalTrails: number;
  totalContents: number;
  totalChallenges: number;
  totalXpDistributed: number;
}

export interface TrailSummary {
  id: number;
  name: string;
  contentCount: number;
  challengeCount: number;
  enrolledCount: number;
}

export interface ModeratorProfile {
  id: string;
  name: string;
  email: string;
  role: "Moderator";
  metrics: PlatformMetrics;
  trails: TrailSummary[];
}

export type ProfileResponse = StudentProfile | ModeratorProfile;

export function isStudentProfile(p: ProfileResponse): p is StudentProfile {
  return p.role !== "Moderator";
}

export function isModeratorProfile(p: ProfileResponse): p is ModeratorProfile {
  return p.role === "Moderator";
}
