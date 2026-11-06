// 前端静态检查配置：TypeScript 推荐规则 + React Hooks 规则，格式化交给 Prettier。
import js from '@eslint/js';
import prettier from 'eslint-config-prettier';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist/**', 'node_modules/**'] },
  js.configs.recommended,
  tseslint.configs.recommended,
  reactHooks.configs.flat.recommended,
  reactRefresh.configs.vite,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser
    },
    rules: {
      // 列表/详情页在挂载后按查询条件重置加载状态属于预期的取数流程，不视为级联渲染。
      'react-hooks/set-state-in-effect': 'off'
    }
  },
  prettier
);
