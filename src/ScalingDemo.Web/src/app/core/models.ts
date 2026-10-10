export type BmiCategory = 'Underweight' | 'Normal' | 'Overweight' | 'Obese';

export interface BmiRequest {
  name: string | null;
  age: number;
  heightCm: number;
  weightKg: number;
}

export interface BmiRecord {
  id: number;
  name: string | null;
  age: number;
  heightCm: number;
  weightKg: number;
  bmi: number;
  category: BmiCategory;
  createdAtUtc: string;
}

export interface BmiResult {
  record: BmiRecord;
  healthyWeightMinKg: number;
  healthyWeightMaxKg: number;
  advice: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface BmiStats {
  total: number;
  averageBmi: number;
  underweight: number;
  normal: number;
  overweight: number;
  obese: number;
}
