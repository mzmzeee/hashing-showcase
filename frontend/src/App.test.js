import { render, screen } from '@testing-library/react';
import App from './App';

test('renders security warning and login heading', () => {
  render(<App />);
  expect(screen.getByText(/educational demo only/i)).toBeInTheDocument();
  expect(screen.getByRole('heading', { name: /login/i })).toBeInTheDocument();
});
