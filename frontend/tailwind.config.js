/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        navy: {
          900: '#12305a',
          800: '#1d4e89',
          700: '#12386b',
        },
        brand: {
          yellow: '#ffc928',
          orange: '#d9431a',
          blue: '#1d4e89',
          navy: '#12305a',
        },
      },
      fontFamily: {
        sans: ['"Plus Jakarta Sans"', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [],
}

