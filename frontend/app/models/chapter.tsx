import Paragraf from "./paragraf";

export default interface Chapter{
    id: string;
    ranobeId: string;
    name: string;
    number: number;
    paragrafs?: Paragraf[];
    isCheck: boolean;
    status?: number;
}