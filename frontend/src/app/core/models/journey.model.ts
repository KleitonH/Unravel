// Espelham os DTOs do backend (Application/Journey/DTOs/JourneyDTOs.cs).
// Mantidos como `type` (não interface) por leveza — nada precisa estender.

export type JourneyReason = "NewLearning" | "DueReview" | "Reinforce";

export type JourneyItem = {
  topicId: number;
  contentId: number;
  slug: string;
  title: string;
  reason: JourneyReason;
  priority: number;
  effectiveMastery: number;
  difficultyScore: number;
};

export type JourneyPlan = {
  userId: string;
  trailId: number;
  trailName: string;
  generatedAt: string;
  metaDia: number;
  today: JourneyItem[];
  upcoming: JourneyItem[];
};
