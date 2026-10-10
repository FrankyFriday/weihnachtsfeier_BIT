import { randomBytes, scryptSync } from 'node:crypto';
import { stdin, stdout } from 'node:process';

if (!stdin.isTTY || typeof stdin.setRawMode !== 'function') {
  throw new Error('Bitte dieses Kommando direkt in einem Terminal ausführen.');
}

let password = '';
stdout.write('Host-Passwort (mindestens 12 Zeichen): ');
stdin.setRawMode(true);
stdin.setEncoding('utf8');
stdin.resume();

stdin.on('data', (input) => {
  for (const character of input) {
    if (character === '\u0003') {
      stdin.setRawMode(false);
      stdout.write('\nAbgebrochen.\n');
      process.exit(1);
    }
    if (character === '\r' || character === '\n') {
      stdin.setRawMode(false);
      stdin.pause();
      stdout.write('\n');
      if (password.length < 12) {
        throw new Error('Das Passwort muss mindestens 12 Zeichen lang sein.');
      }
      const salt = randomBytes(16);
      const digest = scryptSync(password, salt, 64);
      password = '';
      console.log(`HOST_PASSWORD_HASH=scrypt$${salt.toString('base64url')}$${digest.toString('base64url')}`);
      return;
    }
    if (character === '\u007f' || character === '\b') {
      password = Array.from(password).slice(0, -1).join('');
    } else {
      password += character;
    }
  }
});
