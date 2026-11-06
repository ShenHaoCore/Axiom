// 统一错误提示条。
export default function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="banner error" role="alert">
      {message}
    </div>
  );
}
