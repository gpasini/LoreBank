import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Pager } from "./Pager";

describe("Pager", () => {
  it("ne s'affiche pas quand tout tient en une page", () => {
    const { container } = render(
      <Pager page={1} pageSize={20} totalCount={5} onChange={vi.fn()} />,
    );

    expect(container.innerHTML).toBe("");
  });

  it("compte les pages depuis le total et navigue", () => {
    const onChange = vi.fn();

    render(
      <Pager page={2} pageSize={20} totalCount={45} onChange={onChange} />,
    );

    expect(screen.getByText("page 2 sur 3")).toBeDefined();

    fireEvent.click(screen.getByText("Suivant"));

    expect(onChange).toHaveBeenCalledWith(3);
  });

  it("désactive le bord atteint", () => {
    render(<Pager page={3} pageSize={20} totalCount={45} onChange={vi.fn()} />);

    expect((screen.getByText("Suivant") as HTMLButtonElement).disabled).toBe(
      true,
    );
    expect((screen.getByText("Précédent") as HTMLButtonElement).disabled).toBe(
      false,
    );
  });
});
