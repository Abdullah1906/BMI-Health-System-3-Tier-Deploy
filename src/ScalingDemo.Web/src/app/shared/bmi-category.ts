import { BmiCategory, BmiStats } from '../core/models';

interface CategoryMeta {
  readonly cssClass: string;
  readonly range: string;
}

/** Lookup table instead of if/else chains: one place to change a category's look or wording. */
export const CATEGORY_META: Readonly<Record<BmiCategory, CategoryMeta>> = {
  Underweight: { cssClass: 'cat-underweight', range: 'below 18.5' },
  Normal: { cssClass: 'cat-normal', range: '18.5 – 24.9' },
  Overweight: { cssClass: 'cat-overweight', range: '25 – 29.9' },
  Obese: { cssClass: 'cat-obese', range: '30 or above' },
};

export const CATEGORY_ORDER: readonly BmiCategory[] = ['Underweight', 'Normal', 'Overweight', 'Obese'];

export const STATS_KEY: Readonly<Record<BmiCategory, keyof BmiStats>> = {
  Underweight: 'underweight',
  Normal: 'normal',
  Overweight: 'overweight',
  Obese: 'obese',
};

/** The scale drawn on the gauge. Segment widths are proportional to the BMI range they cover. */
export const GAUGE = {
  min: 15,
  max: 40,
  segments: [
    { category: 'Underweight', from: 15, to: 18.5 },
    { category: 'Normal', from: 18.5, to: 25 },
    { category: 'Overweight', from: 25, to: 30 },
    { category: 'Obese', from: 30, to: 40 },
  ] as const,
  ticks: [18.5, 25, 30] as const,
};

export const gaugePercent = (value: number): number =>
  Math.min(100, Math.max(0, ((value - GAUGE.min) / (GAUGE.max - GAUGE.min)) * 100));
