import {randomUUID} from 'node:crypto';

export interface Song {
    id: string;
    title: string;
    artist: string;
    year: number;
}

export interface Player {
    token: string;
    name: string;
    teamId: number | null;
    connected: boolean;
}

export interface Team {
    id: number;
    name: string;
    hue: number;
    timeline: Song[];
    turns: number;
}

export type Phase = 'lobby' | 'placing' | 'reveal' | 'finished';

export interface Reveal {
    song: Song;
    correct: boolean;
    placedIndex: number;
}

export const CARDS_TO_WIN = 10;
export const MAX_TEAMS = 20;

const TEAM_STYLES: [string, number][] = [
    ['Rot', 355], ['Orange', 28], ['Gelb', 48], ['Grün', 140], ['Türkis', 175],
    ['Blau', 218], ['Lila', 275], ['Pink', 330], ['Limette', 90], ['Himmelblau', 200],
    ['Indigo', 250], ['Magenta', 305], ['Koralle', 10], ['Gold', 40], ['Mint', 160],
    ['Marine', 230], ['Violett', 265], ['Rosa', 345], ['Oliv', 70], ['Silber', 210],
];

function hueFor(text: string): number {
    let hue = 0;
    for (const char of text) {
        hue = (hue * 31 + char.codePointAt(0)!) % 360;
    }
    return hue;
}

function shuffle<T>(items: T[]): T[] {
    const copy = [...items];
    for (let i = copy.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [copy[i], copy[j]] = [copy[j], copy[i]];
    }
    return copy;
}

export class Game {
    readonly code: string;
    phase: Phase = 'lobby';
    players: Player[] = [];
    teamCount = 0;
    teams: Team[] = [];
    currentIndex = 0;
    captainToken: string | null = null;
    currentSong: Song | null = null;
    reveal: Reveal | null = null;
    winnerTeamIds: number[] = [];
    private deck: Song[] = [];
    private readonly songs: Song[];

    constructor(code: string, songs: Song[]) {
        this.code = code;
        this.songs = songs;
    }

    get solo(): boolean {
        return this.teamCount === 0;
    }

    get currentTeam(): Team | undefined {
        return this.teams[this.currentIndex];
    }

    addPlayer(name: string, teamId?: number | null): Player {
        const player: Player = {token: randomUUID(), name, teamId: null, connected: true};
        this.players.push(player);
        this.assignTeam(player, teamId);
        return player;
    }

    findPlayer(token: string | null | undefined): Player | undefined {
        return token ? this.players.find((p) => p.token === token) : undefined;
    }

    removePlayer(token: string): void {
        this.players = this.players.filter((p) => p.token !== token);
    }

    setTeamCount(count: number): void {
        this.requireLobby();
        if (!Number.isInteger(count) || count < 0 || count === 1 || count > MAX_TEAMS) {
            throw new Error(`Teamanzahl: 0 (Einzelspiel) oder 2 bis ${MAX_TEAMS}`);
        }
        this.teamCount = count;
        this.teams = Array.from({length: count}, (_, id) => this.createTeam(id));
        for (const player of this.players) {
            player.teamId = null;
            this.assignTeam(player);
        }
    }

    moveToTeam(token: string, teamId: number): void {
        this.requireLobby();
        const player = this.findPlayer(token);
        if (!player) {
            throw new Error('Spieler nicht gefunden');
        }
        if (this.solo || !this.teams.some((t) => t.id === teamId)) {
            throw new Error('Team nicht gefunden');
        }
        player.teamId = teamId;
    }

    membersOf(teamId: number): Player[] {
        return this.players.filter((p) => p.teamId === teamId);
    }

    start(): void {
        this.requireLobby();
        if (this.solo) {
            if (this.players.length < 2) {
                throw new Error('Mindestens 2 Spieler nötig');
            }
            this.teams = this.players.map((p, id) => {
                p.teamId = id;
                return {id, name: p.name, hue: hueFor(p.name), timeline: [], turns: 0};
            });
        } else {
            this.teams = this.teams.filter((t) => this.membersOf(t.id).length > 0);
            if (this.teams.length < 2) {
                throw new Error('Mindestens 2 Teams mit Mitspielern nötig');
            }
        }
        if (this.songs.length < this.teams.length + CARDS_TO_WIN) {
            throw new Error('Zu wenige Songs in data/songs.json (erst npm run import ausführen)');
        }
        this.deck = shuffle(this.songs);
        this.teams = shuffle(this.teams);
        for (const team of this.teams) {
            team.timeline = [this.deck.pop()!];
            team.turns = 0;
        }
        this.currentIndex = 0;
        this.drawCard();
    }

    canAct(token: string | null | undefined): boolean {
        const player = this.findPlayer(token);
        const team = this.currentTeam;
        if (!player || !team || player.teamId !== team.id) {
            return false;
        }
        if (this.captainToken === token) {
            return true;
        }
        const captain = this.findPlayer(this.captainToken);
        return !captain || !captain.connected;
    }

    place(token: string, index: number): Reveal {
        if (this.phase !== 'placing' || !this.currentSong) {
            throw new Error('Gerade kann keine Karte gelegt werden');
        }
        const team = this.currentTeam!;
        if (this.findPlayer(token)?.teamId !== team.id) {
            throw new Error('Dein Team ist nicht dran');
        }
        if (!this.canAct(token)) {
            throw new Error('Nur der Spielführer legt die Karte');
        }
        if (!Number.isInteger(index) || index < 0 || index > team.timeline.length) {
            throw new Error('Ungültige Position');
        }

        const song = this.currentSong;
        const before = team.timeline[index - 1];
        const after = team.timeline[index];
        const correct = (!before || before.year <= song.year) && (!after || song.year <= after.year);
        if (correct) {
            team.timeline.splice(index, 0, song);
        }
        this.reveal = {song, correct, placedIndex: index};
        this.phase = 'reveal';
        return this.reveal;
    }

    next(): void {
        if (this.phase !== 'reveal') {
            throw new Error('Noch nicht aufgedeckt');
        }
        const winners = this.teams.filter((t) => t.timeline.length >= CARDS_TO_WIN);
        if (winners.length > 0) {
            this.finish(winners);
            return;
        }
        if (this.deck.length === 0) {
            const most = Math.max(...this.teams.map((t) => t.timeline.length));
            this.finish(this.teams.filter((t) => t.timeline.length === most));
            return;
        }
        this.currentIndex = (this.currentIndex + 1) % this.teams.length;
        this.drawCard();
    }

    private requireLobby(): void {
        if (this.phase !== 'lobby') {
            throw new Error('Spiel läuft bereits');
        }
    }

    private createTeam(id: number): Team {
        const [label, hue] = TEAM_STYLES[id];
        return {id, name: `Team ${label}`, hue, timeline: [], turns: 0};
    }

    private assignTeam(player: Player, teamId?: number | null): void {
        if (this.solo) {
            player.teamId = null;
            return;
        }
        if (teamId != null && this.teams.some((t) => t.id === teamId)) {
            player.teamId = teamId;
            return;
        }
        const size = (t: Team) => this.membersOf(t.id).filter((p) => p !== player).length;
        player.teamId = this.teams.reduce((best, t) => (size(t) < size(best) ? t : best)).id;
    }

    private pickCaptain(team: Team): string | null {
        const members = this.membersOf(team.id);
        if (members.length === 0) {
            return null;
        }
        const start = team.turns % members.length;
        team.turns++;
        for (let k = 0; k < members.length; k++) {
            const member = members[(start + k) % members.length];
            if (member.connected) {
                return member.token;
            }
        }
        return members[start].token;
    }

    private finish(winners: Team[]): void {
        this.phase = 'finished';
        this.winnerTeamIds = winners.map((t) => t.id);
        this.currentSong = null;
        this.captainToken = null;
    }

    private drawCard(): void {
        this.currentSong = this.deck.pop()!;
        this.reveal = null;
        this.captainToken = this.pickCaptain(this.currentTeam!);
        this.phase = 'placing';
    }

    publicState() {
        const running = this.phase === 'placing' || this.phase === 'reveal';
        return {
            code: this.code,
            phase: this.phase,
            solo: this.solo,
            teamCount: this.teamCount,
            deckSize: this.deck.length,
            cardsToWin: CARDS_TO_WIN,
            currentTeamId: running ? this.currentTeam?.id ?? null : null,
            captainToken: running ? this.captainToken : null,
            winnerTeamIds: this.winnerTeamIds,
            reveal: this.reveal,
            teams: this.teams.map((t) => ({id: t.id, name: t.name, hue: t.hue, timeline: t.timeline})),
            players: this.players.map((p) => ({
                token: p.token,
                name: p.name,
                teamId: p.teamId,
                connected: p.connected,
            })),
        };
    }
}
