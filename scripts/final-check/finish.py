#!/usr/bin/env python3
"""Codespaces verification helper. Writes a report; never commits or pushes."""
import datetime
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import tempfile
import time
import urllib.request

ROOT = Path.cwd()
START = time.monotonic()

def run(args, capture=False, check=True, **kwargs):
    print('\n>>> ' + ' '.join(map(str, args)), flush=True)
    return subprocess.run(args, check=check, text=True, stdout=subprocess.PIPE if capture else None, **kwargs)

def output(args):
    return subprocess.check_output(args, text=True).strip()

def require(condition, message):
    if not condition:
        raise RuntimeError(message)

def healthy(url):
    try:
        with urllib.request.urlopen(url, timeout=3) as r:
            return r.status == 200
    except Exception:
        return False

def wait_health(url, seconds):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        if healthy(url):
            return True
        time.sleep(2)
    return False

def main():
    require(os.environ.get('CODESPACES') == 'true', 'Run this script inside GitHub Codespaces, not on your physical PC.')
    require((ROOT / 'docker-compose.yml').exists(), 'Run from /workspaces/northwind-support.')
    lock = json.loads((ROOT / 'package-lock.json').read_text())
    require(lock['packages']['node_modules/source-map-js']['version'] == '1.2.2', 'Expected reviewed source-map-js 1.2.2 fix; share output before proceeding.')
    run(['npm', 'ci'])
    run(['npm', 'audit'])
    run(['npm', 'run', 'test', '--workspace', 'apps/web', '--', '--run'])
    run(['npm', 'run', 'typecheck'])
    run(['npm', 'run', 'lint'])
    run(['npm', 'run', 'build'])
    # Preserve .env and all existing database volumes.
    run(['npm', 'run', 'setup'])
    run(['docker', 'compose', 'up', '-d'])
    if not wait_health('http://127.0.0.1:5080/health', 20):
        # Reapply only the previously diagnosed Codespaces workaround, scoped to this bridge.
        networks = json.loads(output(['docker', 'inspect', 'supportdesk-sql']))[0]['NetworkSettings']['Networks']
        require('northwind-support_default' in networks, 'Unexpected Docker network; no firewall rules changed.')
        network = json.loads(output(['docker', 'network', 'inspect', 'northwind-support_default']))[0]
        require(network['Driver'] == 'bridge', 'Expected Docker bridge driver.')
        bridge = network.get('Options', {}).get('com.docker.network.bridge.name') or 'br-' + network['Id'][:12]
        require(re.fullmatch(r'[a-zA-Z0-9_.-]+', bridge) is not None, 'Invalid bridge name.')
        rules = run(['sudo', '-n', 'iptables-legacy', '-S', 'FORWARD'], capture=True)
        require('-P FORWARD DROP' in rules.stdout, 'Health failed without the known legacy DROP problem. Share docker compose logs --tail=50 api.')
        rule = ['DOCKER-USER', '-i', bridge, '-o', bridge, '-j', 'ACCEPT']
        existing = run(['sudo', '-n', 'iptables-legacy', '-C', *rule], check=False)
        if existing.returncode:
            run(['sudo', '-n', 'iptables-legacy', '-I', *rule])
        run(['docker', 'compose', 'restart', 'api'])
        require(wait_health('http://127.0.0.1:5080/health', 120), 'API health still fails. Stop and share API logs.')
    with tempfile.TemporaryDirectory(prefix='northwind-verification-') as temporary:
        temp = Path(temporary)
        run(['npm', 'install', '--prefix', str(temp), '--no-audit', '--no-fund', '@playwright/test'])
        cli = temp / 'node_modules/playwright/cli.js'
        run(['node', str(cli), 'install', '--with-deps', 'chromium'])
        env = os.environ.copy()
        env.update(VITE_API_PROXY_TARGET='http://127.0.0.1:5080', NORTHWIND_TOOLS=str(temp), NORTHWIND_RESULT=str(temp / 'result.json'))
        with (temp / 'vite.log').open('w') as log:
            server = subprocess.Popen(['npm', 'run', 'dev', '--workspace', 'apps/web', '--', '--host', '127.0.0.1', '--port', '5179', '--strictPort'], env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                require(wait_health('http://127.0.0.1:5179', 30) and server.poll() is None, 'Dedicated Vite server failed. Ensure port 5179 is free.')
                run(['node', 'scripts/final-check/browser.cjs'], env=env, timeout=240)
                result = json.loads((temp / 'result.json').read_text())
                require(result.get('passed') is True, 'Browser checks did not pass.')
                playwright_version = json.loads((temp / 'node_modules/@playwright/test/package.json').read_text())['version']
            finally:
                if server.poll() is None:
                    os.killpg(server.pid, signal.SIGTERM)
                    server.wait(timeout=15)
                print('Vite log tail:\n' + '\n'.join((temp / 'vite.log').read_text().splitlines()[-12:]))
    duration = round((time.monotonic() - START) / 60, 1)
    stamp = datetime.datetime.now(datetime.timezone.utc).isoformat()
    report = '# Final automated verification\n\n'
    report += f'Completed: {stamp}. Runner elapsed time: {duration} minutes (includes installation/download waits; not personal effort).\n\n'
    report += f'Playwright {playwright_version}; Chromium {result["browser"]}; real React UI and live SQL-backed API in Codespaces. No mocked API responses.\n\n'
    report += '- npm ci and npm audit: passed; zero reported vulnerabilities at run time.\n- Frontend tests, typecheck, lint, and combined backend/frontend build: passed.\n'
    report += '\n'.join('- ' + item for item in result['checks']) + '\n\n'
    report += f'Development test ticket retained: `{result["ticket"]}`. Each rerun creates one labelled ticket and one escalation. Existing tickets and volumes are not deleted.\n\n'
    report += 'These are automated Chromium smoke checks, not a claim of manual visual, accessibility, cross-browser or exhaustive business-rule testing. Earlier backend tests and Docker measurements are documented in README and APPROACH. Candidate understanding/interview preparation cannot be verified by this script.\n'
    (ROOT / 'FINAL_VERIFICATION.md').write_text(report)
    print('\nSUCCESS: verification passed; report written to FINAL_VERIFICATION.md.')
    print('No commits or pushes were performed. Review the report before committing it.')

if __name__ == '__main__':
    try:
        main()
    except (Exception, KeyboardInterrupt) as error:
        print('\nSTOPPED: ' + str(error), file=sys.stderr)
        print('Share the failed check output; no commit or push was performed.', file=sys.stderr)
        sys.exit(1)
