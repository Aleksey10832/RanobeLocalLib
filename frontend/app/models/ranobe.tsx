import Chapter from "./chapter";

export default interface Ranobe {
  id: string;
  name: string;
  pages: number;
  chapters?: Chapter[];
  status?: number;
  privateContent: boolean;
}