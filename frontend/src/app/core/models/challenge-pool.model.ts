// Espelham os DTOs em Application/Forge/DTOs/ChallengePoolDTOs.cs.

export type PoolChallenge = {
  id: number;
  strategy:
    | "Cloze"
    | "Definition"
    | "TrueFalse"
    | "Ordering"
    | "Match"
    | "Code";
  prompt: string;
  options: string[];
  correctIndex: number;
  explanation: string | null;
  estimatedDifficulty: number;
};

export type ChallengePool = {
  contentId: number;
  contentTitle: string;
  trailId: number;
  targetUserMastery: number;
  challenges: PoolChallenge[];
};
