import { existsSync } from 'node:fs';
import { loadEnvFile } from 'node:process';
import { spawn } from 'node:child_process';
if (existsSync('.env')) loadEnvFile('.env');
const child = spawn('dotnet', ['run', '--project', 'apps/api/src/SupportDesk.Presentation', '--launch-profile', 'http'], { stdio: 'inherit', env: process.env });
child.on('error', error => { console.error(error.message); process.exitCode = 1; });
child.on('exit', code => { process.exitCode = code ?? 1; });
