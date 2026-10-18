#!/usr/bin/env node
// הדגמה חיה של התחרות על המשאב המוגבל, מול שרת שרץ.
//
//   node scripts/race-demo.mjs                      (ברירת מחדל: 8 משתתפות על מקום אחד)
//   node scripts/race-demo.mjs --users 12 --capacity 3
//   node scripts/race-demo.mjs --api http://localhost:5080
//
// מה הסקריפט עושה:
//   1. מתחבר כמנהלת ויוצר מפגש חדש עם מספר מקומות קטן.
//   2. רושם למערכת משתתפות חדשות ומתחבר בשם כל אחת מהן.
//   3. שולח את כל בקשות ההרשמה למפגש באותו רגע.
//   4. מדפיס מה כל אחת קיבלה, ובודק שמספר האישורים לא עבר את מספר המקומות.
//
// דורש Node 18 ומעלה (fetch מובנה). אין תלויות חיצוניות.

const args = parseArgs(process.argv.slice(2));
const API = (args.api ?? process.env.WORKSHOPHUB_API ?? 'http://localhost:5080').replace(/\/$/, '');
const USERS = Number(args.users ?? 8);
const CAPACITY = Number(args.capacity ?? 1);

// פרטי משתמשת הדמו (נוצרת ב-seed של סביבת הפיתוח ומתועדת ב-README)
const ADMIN_EMAIL = args['admin-email'] ?? 'admin@workshophub.local';
const ADMIN_PASSWORD = args['admin-password'] ?? 'Admin#2026!';

function parseArgs(argv) {
  const parsed = {};
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i].startsWith('--')) {
      parsed[argv[i].slice(2)] = argv[i + 1];
      i += 1;
    }
  }
  return parsed;
}

async function call(method, path, { token, body } = {}) {
  const response = await fetch(`${API}${path}`, {
    method,
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const text = await response.text();
  return { status: response.status, data: text ? JSON.parse(text) : null };
}

async function expectOk(promise, what) {
  const result = await promise;
  if (result.status < 200 || result.status >= 300) {
    throw new Error(`${what} failed with ${result.status}: ${JSON.stringify(result.data)}`);
  }
  return result.data;
}

async function main() {
  console.log(`API: ${API}`);
  console.log(`${USERS} participants will compete for ${CAPACITY} seat(s)\n`);

  // 1. מנהלת יוצרת מפגש חדש
  const admin = await expectOk(
    call('POST', '/api/tokens', { body: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD } }),
    'Admin login',
  );
  const workshops = await expectOk(call('GET', '/api/workshops?pageSize=1'), 'Loading workshops');
  if (workshops.items.length === 0) {
    throw new Error('No workshops found. Start the API in Development so the demo data is seeded.');
  }
  const workshop = workshops.items[0];

  const session = await expectOk(
    call('POST', `/api/workshops/${workshop.id}/sessions`, {
      token: admin.accessToken,
      body: {
        startsAtUtc: new Date(Date.now() + 14 * 24 * 60 * 60 * 1000).toISOString(),
        durationMinutes: 60,
        location: 'Race demo',
        capacity: CAPACITY,
      },
    }),
    'Creating the session',
  );
  console.log(`Created session ${session.id} for workshop "${workshop.title}" with capacity ${session.capacity}`);

  // 2. משתתפות חדשות נרשמות למערכת ומתחברות
  const runId = Date.now().toString(36);
  const password = `Race#${runId}!`;
  const tokens = [];
  for (let i = 1; i <= USERS; i += 1) {
    const email = `race-${runId}-${i}@workshophub.local`;
    await expectOk(
      call('POST', '/api/users', { body: { fullName: `Race participant ${i}`, email, password } }),
      `Registering participant ${i}`,
    );
    const login = await expectOk(call('POST', '/api/tokens', { body: { email, password } }), `Login of participant ${i}`);
    tokens.push(login.accessToken);
  }
  console.log(`Registered and logged in ${USERS} participants\n`);

  // 3. כולן לוחצות "הרשמה" באותו רגע
  console.log('Sending all registration requests at once...');
  const responses = await Promise.all(
    tokens.map((token) =>
      call('POST', `/api/sessions/${session.id}/registrations`, { token, body: { joinWaitlistIfFull: false } }),
    ),
  );

  // 4. תוצאות
  console.log('\n#   status  result');
  responses.forEach((response, index) => {
    const detail =
      response.status === 201
        ? `registration ${response.data.id} (${response.data.status})`
        : `${response.data?.code} [correlationId ${response.data?.correlationId}]`;
    console.log(`${String(index + 1).padEnd(3)} ${response.status}     ${detail}`);
  });

  const created = responses.filter((r) => r.status === 201).length;
  const conflicts = responses.filter((r) => r.status === 409).length;
  const others = responses.length - created - conflicts;

  const finalSession = await expectOk(call('GET', `/api/sessions/${session.id}`), 'Loading the final session state');

  console.log('\nSummary');
  console.log(`  201 Created : ${created}`);
  console.log(`  409 Conflict: ${conflicts}`);
  console.log(`  other       : ${others}`);
  console.log(`  session now : ${finalSession.seatsTaken}/${finalSession.capacity} seats taken`);

  const consistent = created === finalSession.seatsTaken && finalSession.seatsTaken <= finalSession.capacity && others === 0;
  console.log(
    consistent
      ? '\nOK: the session was not oversold, and every losing request received a clean 409.'
      : '\nPROBLEM: the numbers do not add up - check the server log.',
  );
  console.log('The conflicts are logged at Warning level in WorkshopHub.API/logs/warnings-<date>.log');

  process.exitCode = consistent ? 0 : 1;
}

main().catch((error) => {
  console.error(`\nRace demo failed: ${error.message}`);
  process.exitCode = 1;
});
