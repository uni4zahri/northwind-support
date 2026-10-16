import { saveSession, useSession } from './auth/session';
import { Link, NavLink, Outlet } from 'react-router-dom';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const session = useSession();
  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <span>Signed in as {session?.user.fullName}</span>
        <button className="button" onClick={() => saveSession(null)}>Log out</button>
        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
