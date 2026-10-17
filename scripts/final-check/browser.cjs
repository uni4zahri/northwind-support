// Real Chromium + live API smoke checks. No mocked network responses.
const { chromium, expect } = require(process.env.NORTHWIND_TOOLS + '/node_modules/@playwright/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = 'http://127.0.0.1:5179';
const checks = [];
const pass = message => { checks.push(message); console.log('PASS:', message); };
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  page.setDefaultTimeout(20000);
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  let created;
  try {
    await page.goto(base + '/tickets');
    await expect(page).toHaveURL(/\/login$/);
    await page.getByLabel('Email', {exact: true}).fill('alex.turner@northwind-support.example');
    await page.getByLabel('Password', {exact: true}).fill('Northwind_Local_Only_2026!');
    const loginWait = page.waitForResponse(r => new URL(r.url()).pathname === '/api/auth/login' && r.request().method() === 'POST');
    await page.getByRole('button', {name:'Sign in', exact:true}).click();
    const loginResponse = await loginWait;
    assert.equal(loginResponse.status(), 200);
    const login = await loginResponse.json();
    await expect(page).toHaveURL(/\/tickets$/);
    await expect(page.locator('#filter-search')).toBeVisible();
    pass('Protected route redirects to login; browser login opens ticket list');
    const headers = {Authorization: 'Bearer ' + login.token};
    async function api(path) {
      const r = await page.request.get(base + '/api' + path, {headers});
      assert.equal(r.status(), 200, path);
      return r.json();
    }
    const categories = await api('/categories');
    const customers = await api('/customers');
    const category = categories.find(c => c.isActive && !c.forcesCriticalPriority);
    assert(category, 'An active non-critical category is required');
    const customer = customers[0];
    assert(customer, 'A seeded customer is required');
    const title = 'Automated verification ' + new Date().toISOString();
    await page.goto(base + '/tickets/new');
    await page.locator('#ticket-title').fill(title);
    await page.locator('#ticket-description').fill('Automated assessment smoke-test ticket. Safe to retain as development test data.');
    await expect(page.locator('#ticket-category option[value="' + category.id + '"]')).toHaveCount(1);
    await page.locator('#ticket-customer').selectOption(String(customer.id));
    await page.locator('#ticket-category').selectOption(String(category.id));
    const createWait = page.waitForResponse(r => new URL(r.url()).pathname === '/api/tickets' && r.request().method() === 'POST');
    await page.getByRole('button', {name:'Raise ticket', exact:true}).click();
    const response = await createWait;
    assert.equal(response.status(), 201);
    created = await response.json();
    assert.equal(created.priority, 'Medium');
    assert.equal(created.title, title);
    assert(created.triage?.assignmentReason, 'Assignment explanation missing');
    assert(created.triage?.slaWindowReason, 'SLA explanation missing');
    assert(Date.parse(created.dueAtUtc) > Date.parse(created.createdAtUtc));
    await expect(page.getByRole('status')).toContainText(created.reference);
    await expect(page.getByRole('status')).toContainText('Priority: Medium');
    await expect(page.getByRole('status')).toContainText(created.assignedAgent?.fullName ?? 'Unassigned');
    pass('Browser creates ticket with default Medium priority, future SLA and visible assignment explanation');
    console.log('Development test ticket retained:', created.reference);
    await page.goto(base + '/tickets');
    async function filter(action, parameters, count) {
      const responseWait = page.waitForResponse(r => {
        const u = new URL(r.url());
        return r.request().method() === 'GET' && u.pathname === '/api/tickets' &&
          Object.entries(parameters).every(([key,value]) => u.searchParams.get(key) === value);
      });
      await action();
      const r = await responseWait;
      assert.equal(r.status(), 200);
      const result = await r.json();
      assert.equal(result.totalCount, count);
      assert.equal(result.items.length, count);
      if (count) assert.equal(result.items[0].id, created.id);
      await expect(page.locator('tbody tr')).toHaveCount(count);
      if (count) await expect(page.locator('tbody')).toContainText(created.reference);
    }
    const query = {search:title};
    await filter(() => page.locator('#filter-search').fill(title), query, 1);
    for (const [id, key, value] of [
      ['filter-status','status','New'], ['filter-priority','priority','Medium'],
      ['filter-category','categoryId',String(category.id)],
      ['filter-customer','customerId',String(customer.id)],
      ['filter-sla','slaStatus','WithinSla']
    ]) {
      query[key] = value;
      await filter(() => page.locator('#' + id).selectOption(value), query, 1);
    }
    if (created.assignedAgent) {
      query.assignedAgentId = String(created.assignedAgent.id);
      await filter(() => page.locator('#filter-agent').selectOption(query.assignedAgentId), query, 1);
      query.unassignedOnly = 'true';
      await filter(() => page.getByLabel('Unassigned only').check(), query, 0);
      query.unassignedOnly = 'false';
      await filter(() => page.getByLabel('Unassigned only').uncheck(), query, 1);
    } else {
      query.unassignedOnly = 'true';
      await filter(() => page.getByLabel('Unassigned only').check(), query, 1);
    }
    query.priority = 'High';
    await filter(() => page.locator('#filter-priority').selectOption('High'), query, 0);
    query.priority = 'Medium';
    await filter(() => page.locator('#filter-priority').selectOption('Medium'), query, 1);
    pass('Browser search and combined status/priority/category/customer/SLA filters update both SQL results and displayed rows; unassigned filter checked');
    await page.getByRole('link', {name:created.reference, exact:true}).click();
    await expect(page.getByRole('heading', {name:title, exact:true})).toBeVisible();
    const reason = 'Automated assessment verification of escalation history';
    await page.locator('#escalation-reason').fill(reason);
    const escalateWait = page.waitForResponse(r => new URL(r.url()).pathname === '/api/tickets/' + created.id + '/escalate' && r.request().method() === 'POST');
    await page.getByRole('button', {name:'Escalate ticket', exact:true}).click();
    const escalatedResponse = await escalateWait;
    assert.equal(escalatedResponse.status(), 200);
    const escalated = await escalatedResponse.json();
    assert.equal(escalated.ticket.priority, 'High');
    assert.equal(escalated.escalation.fromPriority, 'Medium');
    assert.equal(escalated.escalation.toPriority, 'High');
    assert.equal(escalated.escalation.reason, reason);
    assert.notEqual(escalated.ticket.dueAtUtc, created.dueAtUtc);
    assert(Date.parse(escalated.ticket.dueAtUtc) > Date.now());
    await expect(page.locator('ol')).toContainText('Medium → High');
    await expect(page.locator('ol')).toContainText(reason);
    await page.reload();
    await expect(page.locator('ol')).toContainText(reason);
    const history = await api('/tickets/' + created.id + '/escalations');
    assert.equal(history.length, 1);
    assert.equal(history[0].reason, reason);
    pass('Browser escalation raises Medium to High, changes due time and persists history across reload');
    await page.getByRole('button', {name:'Log out', exact:true}).click();
    await expect(page).toHaveURL(/\/login$/);
    await page.goto(base + '/tickets/' + created.id);
    await expect(page).toHaveURL(/\/login$/);
    const anonymous = await page.request.get(base + '/api/tickets');
    assert.equal(anonymous.status(), 401);
    assert.deepEqual(errors, [], 'Browser JavaScript errors');
    pass('Logout protects direct detail navigation; anonymous API remains 401; no uncaught browser errors');
    fs.writeFileSync(process.env.NORTHWIND_RESULT, JSON.stringify({passed:true, at:new Date().toISOString(), checks, ticket:created.reference, browser:browser.version()}, null, 2));
  } catch (e) {
    console.error('BROWSER CHECK FAILED:', e.message);
    if (created) console.error('Test ticket retained:', created.reference);
    process.exitCode = 1;
  } finally { await browser.close(); }
})().catch(e => { console.error(e.message); process.exitCode = 1; });
