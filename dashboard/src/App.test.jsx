import { render, screen } from '@testing-library/react';
import App from './App.jsx';

describe('App', () => {
  it('renders the brand logo', () => {
    render(<App />);
    expect(screen.getByAltText('My Morning News')).toBeInTheDocument();
  });
});
