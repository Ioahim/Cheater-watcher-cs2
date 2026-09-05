export function Footer() {
  return (
    <footer className="mt-auto">
      <div className="mx-auto flex w-full max-w-6xl items-center justify-center px-4 py-6">
        <p className="text-center text-xs text-faint">
          Powered by{" "}
          <a
            href="https://leetify.com"
            target="_blank"
            rel="noreferrer"
            className="transition-colors hover:text-primary-light"
          >
            Leetify
          </a>
        </p>
      </div>
    </footer>
  );
}