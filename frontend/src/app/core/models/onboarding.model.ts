// Espelham os DTOs em Application/Journey/Onboarding/OnboardingDTOs.cs.

export type LevelingQuestion = {
  topicId: number;
  contentId: number;
  contentTitle: string;
  strategy: string;
  prompt: string;
  options: string[];
  difficultyTarget: number;
};

export type LevelingTrailGroup = {
  trailId: number;
  trailName: string;
  questions: LevelingQuestion[];
};

export type OnboardingTest = {
  trails: LevelingTrailGroup[];
};

export type LevelingAnswer = {
  topicId: number;
  selectedOptionIndex: number;
};

export type OnboardingSubmit = {
  answers: LevelingAnswer[];
};

export type TrailLevelEstimate = {
  trailId: number;
  trailName: string;
  estimatedMastery: number;
  label: "Iniciante" | "Intermediário" | "Avançado";
};

export type OnboardingResult = {
  estimates: TrailLevelEstimate[];
  enrolledTrailIds: number[];
};
